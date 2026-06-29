using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.AncestralPortraitManagement.Models;
using Odin.Api.Endpoints.ImageGenerationManagement;
using Odin.Api.Endpoints.Payments;
using Odin.Api.Extensions;
using Odin.Api.Storage;

namespace Odin.Api.Endpoints.AncestralPortraitManagement;

/// <inheritdoc cref="IAncestralPortraitService"/>
public sealed class AncestralPortraitService(
    ApplicationDbContext dbContext,
    IOpenAIImageClient openAiClient,
    IAncestralPortraitSettingsService settingsService,
    IR2Storage r2Storage,
    IAppStorePurchaseService appStore,
    IBackgroundJobClient backgroundJobs,
    IOptions<AppleIapOptions> appleOptions,
    Odin.Api.Hubs.IAppStorePurchaseRealtimeNotifier purchaseLiveUpdates,
    ILogger<AncestralPortraitService> logger) : IAncestralPortraitService
{
    private readonly AppleIapOptions _apple = appleOptions.Value;

    /// A Running set whose <c>UpdatedAt</c> is older than this is considered stale (worker died mid-run) and may be
    /// re-claimed/regenerated. Far longer than a real run (~1–3 min); matches the Hangfire invisibility timeout.
    private const int StaleRunMinutes = 30;

    public async Task<(AncestralPortraitSetContract.Response Response, int StatusCode, string? Error)> PurchaseAsync(
        int orderId, string identityId, string transactionJws, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.QpadmOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
            return (Empty(orderId), 404, $"Order {orderId} not found.");
        if (order.CreatedBy != identityId)
            return (Empty(orderId), 403, "You do not own this order.");

        var user = await dbContext.Users.RequireByIdentityAsync(identityId, cancellationToken);

        // Validate the StoreKit add-on purchase (throws AppStorePurchaseException → 400 at the endpoint).
        var verified = appStore.ValidateAddOnTransaction(transactionJws, _apple.AiPortraitsProductId);

        // Re-purchase is intentional: each NEW Apple transaction creates a NEW set (a fresh iteration the user keeps).
        // Only a REPLAY of the SAME transaction (e.g. a retried request / app-killed-mid-flow) is idempotent — it
        // returns the set that transaction already created (anti double-create, the unique TransactionId guarantees it).
        var byTxn = await dbContext.AncestralPortraitSets
            .FirstOrDefaultAsync(s => s.TransactionId == verified.TransactionId, cancellationToken);
        if (byTxn is not null)
        {
            var reloaded = await LoadSetByIdAsync(byTxn.Id, cancellationToken);
            return (MapSet(reloaded!, byTxn.OrderId), 200, null);
        }

        var now = DateTime.UtcNow;
        var set = new AncestralPortraitSet
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            UserId = user.Id,
            TransactionId = verified.TransactionId,
            Status = AncestralPortraitStatus.Pending,
            CreatedBy = identityId,
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.AncestralPortraitSets.Add(set);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent request replaying the SAME Apple transaction won the unique-TransactionId race. Return the
            // set it created (idempotent) instead of surfacing a 500. Mirrors OrderService.CreatePaidAsync.
            dbContext.Entry(set).State = EntityState.Detached;
            var winner = await dbContext.AncestralPortraitSets.AsNoTracking().Include(s => s.Portraits)
                .FirstOrDefaultAsync(s => s.TransactionId == verified.TransactionId, cancellationToken);
            if (winner is not null)
                return (MapSet(winner, winner.OrderId), 200, null);
            throw;
        }

        backgroundJobs.Enqueue<IAncestralPortraitWorker>(w => w.RunAsync(set.Id, CancellationToken.None));

        // Live-push the new add-on purchase to the admin "App Store Transactions" page (best-effort; the notifier
        // swallows its own failures so a live-refresh hiccup never fails the purchase).
        await purchaseLiveUpdates.NotifyPurchaseRecordedAsync(
            kind: "AiPortraits",
            productLabel: "Through the Ages",
            amount: _apple.AiPortraitsPrice,
            currency: _apple.Currency,
            createdBySub: identityId,
            cancellationToken: cancellationToken);

        return (MapSet(set, orderId), 201, null);
    }

    public async Task<(List<AncestralPortraitSetContract.Response>? Response, int StatusCode)> ListSetsAsync(
        int orderId, string identityId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.QpadmOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
            return (null, 404);
        if (order.CreatedBy != identityId)
            return (null, 403);

        var user = await dbContext.Users.RequireByIdentityAsync(identityId, cancellationToken);
        var sets = await dbContext.AncestralPortraitSets.AsNoTracking().Include(s => s.Portraits)
            .Where(s => s.OrderId == orderId && s.UserId == user.Id)
            .OrderByDescending(s => s.CreatedAt)   // newest iteration first
            .ToListAsync(cancellationToken);
        return (sets.Select(s => MapSet(s, orderId)).ToList(), 200);
    }

    public async Task<(AncestralPortraitSetContract.Response? Response, int StatusCode)> GetSetByIdAsync(
        Guid setId, string identityId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.RequireByIdentityAsync(identityId, cancellationToken);
        var set = await LoadSetByIdAsync(setId, cancellationToken);
        if (set is null) return (null, 404);
        if (set.UserId != user.Id) return (null, 403);
        return (MapSet(set, set.OrderId), 200);
    }

    public async Task<int> DeleteSetAsync(Guid setId, string identityId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.RequireByIdentityAsync(identityId, cancellationToken);
        var set = await dbContext.AncestralPortraitSets.Include(s => s.Portraits)
            .FirstOrDefaultAsync(s => s.Id == setId, cancellationToken);
        if (set is null) return 404;
        if (set.UserId != user.Id) return 403;
        // Don't delete a set that's mid-generation: the worker uploads R2 objects then commits the rows at the end,
        // so tearing the set out from under it would orphan those uploads + fail the worker's final save. 409 → the
        // user waits for it to finish. (A *stale* Running set — a dead worker — is deletable so it can be cleaned up.)
        if (set.Status == AncestralPortraitStatus.Running && set.UpdatedAt >= DateTime.UtcNow.AddMinutes(-StaleRunMinutes))
            return 409;

        // Delete the PRIVATE R2 objects (images of the user's face) before removing the rows — leaving them would
        // orphan biometric data in storage.
        foreach (var portrait in set.Portraits)
            await r2Storage.DeleteAsync(portrait.R2Key, cancellationToken);
        var removed = set.Portraits.Count;

        dbContext.AncestralPortraitSets.Remove(set); // cascade-deletes the portrait rows
        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Ancestral portrait set {SetId} deleted ({Count} R2 object(s) removed).", setId, removed);
        return 200;
    }

    public async Task<int> RequestGenerateAsync(Guid setId, string identityId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.RequireByIdentityAsync(identityId, cancellationToken);
        var set = await dbContext.AncestralPortraitSets
            .FirstOrDefaultAsync(s => s.Id == setId, cancellationToken);
        if (set is null) return 404;
        if (set.UserId != user.Id) return 403;
        // Already generating (and not stale) → don't double-enqueue. A stale Running set (dead worker) IS re-enqueued
        // so it can recover — the claim's staleness check lets exactly one re-run proceed.
        if (set.Status == AncestralPortraitStatus.Running && set.UpdatedAt >= DateTime.UtcNow.AddMinutes(-StaleRunMinutes))
            return 202;

        backgroundJobs.Enqueue<IAncestralPortraitWorker>(w => w.RunAsync(set.Id, CancellationToken.None));
        return 202;
    }

    /// <summary>Toggle this variation's selection (multi-select: the user can pick any subset per era, even all
    /// of them, or none). 200 / 403 / 404.</summary>
    public async Task<int> SelectAsync(int portraitId, string identityId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.RequireByIdentityAsync(identityId, cancellationToken);
        var portrait = await dbContext.AncestralPortraits.Include(p => p.Set)
            .FirstOrDefaultAsync(p => p.Id == portraitId, cancellationToken);
        if (portrait is null) return 404;
        if (portrait.Set.UserId != user.Id) return 403;

        portrait.IsSelected = !portrait.IsSelected;
        await dbContext.SaveChangesAsync(cancellationToken);
        return 200;
    }

    public async Task<(byte[]? Bytes, string? ContentType, int StatusCode)> GetPortraitBytesAsync(
        int portraitId, string identityId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.RequireByIdentityAsync(identityId, cancellationToken);
        var portrait = await dbContext.AncestralPortraits.AsNoTracking().Include(p => p.Set)
            .FirstOrDefaultAsync(p => p.Id == portraitId, cancellationToken);
        if (portrait is null) return (null, null, 404);
        if (portrait.Set.UserId != user.Id) return (null, null, 403);

        var bytes = await r2Storage.DownloadAsync(portrait.R2Key, cancellationToken);
        return bytes is null ? (null, null, 404) : (bytes, portrait.ContentType, 200);
    }

    public async Task RunGenerationAsync(Guid setId, CancellationToken cancellationToken = default)
    {
        var set = await dbContext.AncestralPortraitSets.Include(s => s.Portraits)
            .FirstOrDefaultAsync(s => s.Id == setId, cancellationToken);
        if (set is null)
        {
            logger.LogWarning("Ancestral portrait generation for unknown set {SetId}.", setId);
            return;
        }
        // Atomically CLAIM the run (Pending/Failed → Running) so a duplicate enqueue or a second concurrent worker
        // can't generate the SAME set twice — that doubled the portraits. The conditional UPDATE only succeeds for
        // exactly one worker; a set that's already Running or Succeeded is skipped (claimed == 0). A **stale** Running
        // set (UpdatedAt older than the staleness window — e.g. the worker process died mid-run and Hangfire's
        // invisibility timeout re-queued the job) IS re-claimable, so a crashed generation self-heals instead of being
        // stuck "Painting…" forever. The window (30 min) is far longer than a real run (~1–3 min) and matches the
        // Hangfire invisibility timeout, so a legitimately in-flight run is never re-claimed (no double-run).
        var staleBefore = DateTime.UtcNow.AddMinutes(-StaleRunMinutes);
        var claimed = await dbContext.AncestralPortraitSets
            .Where(s => s.Id == setId
                && s.Status != AncestralPortraitStatus.Succeeded
                && (s.Status != AncestralPortraitStatus.Running || s.UpdatedAt < staleBefore))
            .ExecuteUpdateAsync(u => u
                .SetProperty(s => s.Status, AncestralPortraitStatus.Running)
                .SetProperty(s => s.Error, (string?)null)
                .SetProperty(s => s.UpdatedAt, DateTime.UtcNow), cancellationToken);
        if (claimed == 0)
        {
            logger.LogInformation("Ancestral portrait set {SetId} already running/finished — skipping duplicate generation.", set.Id);
            return;
        }
        set.Status = AncestralPortraitStatus.Running;
        set.Error = null;

        logger.LogInformation("Ancestral portrait generation STARTED for set {SetId} (order {OrderId}, user {UserId}).",
            set.Id, set.OrderId, set.UserId);

        // R2 keys uploaded THIS run (declared outside the try so the catch can reclaim them). The portrait rows are
        // only persisted at the single final SaveChanges (all-or-nothing), so anything that throws before that commit
        // orphans these objects — the catch deletes them so we never strand private face images in storage.
        var uploadedKeys = new List<string>();
        try
        {
            // Admin-editable settings (model/quality/size/variations/caps/cost rates) — fully runtime-configurable.
            var settings = await settingsService.GetAsync(cancellationToken);
            var user = await dbContext.Users.FirstAsync(u => u.Id == set.UserId, cancellationToken);

            // Face reference photos (the user's own) → the model's edit inputs.
            var facePhotos = await dbContext.UserFacePhotos.AsNoTracking()
                .Where(p => p.UserId == set.UserId).OrderBy(p => p.Id)
                .Take(settings.MaxFaceReferences).ToListAsync(cancellationToken);
            var faceRefs = new List<OpenAIReferenceImage>();
            foreach (var photo in facePhotos)
            {
                var bytes = await r2Storage.DownloadAsync(photo.R2Key, cancellationToken);
                if (bytes is not null)
                    faceRefs.Add(new OpenAIReferenceImage(bytes, photo.ContentType, photo.OriginalFileName));
            }
            logger.LogInformation("Ancestral portraits set {SetId}: loaded {Count} face reference(s) of {Available} photo(s).",
                set.Id, faceRefs.Count, facePhotos.Count);
            if (faceRefs.Count == 0)
            {
                await FailAsync(set, "Capture your face photos first (Settings → AI Face Capture).", cancellationToken);
                return;
            }

            // The order's qpAdm eras (every population in every era becomes its own portrait group — see below).
            var order = await dbContext.QpadmOrders.AsNoTracking()
                .Include(o => o.GeneticInspection)
                .FirstOrDefaultAsync(o => o.Id == set.OrderId, cancellationToken);
            if (order?.GeneticInspection is null)
            {
                await FailAsync(set, "This order has no results yet.", cancellationToken);
                return;
            }
            var qpadm = await dbContext.QpadmResults.AsNoTracking().AsSplitQuery()
                .Include(qr => qr.QpadmResultEraGroups).ThenInclude(eg => eg.Era)
                .Include(qr => qr.QpadmResultEraGroups).ThenInclude(eg => eg.QpadmResultPopulations).ThenInclude(p => p.Population)
                .FirstOrDefaultAsync(qr => qr.GeneticInspectionId == order.GeneticInspection.Id, cancellationToken);
            if (qpadm is null)
            {
                await FailAsync(set, "This order has no qpAdm result.", cancellationToken);
                return;
            }

            // Clear any prior portraits (re-generation) — R2 objects + rows.
            if (set.Portraits.Count > 0)
            {
                foreach (var prior in set.Portraits)
                    await r2Storage.DeleteAsync(prior.R2Key, cancellationToken);
                dbContext.AncestralPortraits.RemoveRange(set.Portraits);
                set.Portraits.Clear();
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            var parameters = new OpenAIImageParameters(
                settings.Model, settings.Size, settings.Quality, settings.Background, settings.OutputFormat, null,
                settings.Moderation, settings.VariationsPerEra, user.IdentityId);
            var (contentType, ext) = ResolveContentType(settings.OutputFormat);

            var identitySlug = SanitizeIdentity(user.IdentityId);
            // De-dup era groups by EraId (defensive — a duplicate era group in the source would otherwise produce
            // duplicate portrait groups) and take a stable, ordered slice.
            var eras = qpadm.QpadmResultEraGroups
                .GroupBy(g => g.EraId).Select(g => g.First())
                .OrderBy(g => g.EraId)
                .Take(settings.MaxEras).ToList();

            // Flatten to one (era, population) target per population in every era, ranked by ancestry % within the
            // era and capped at MaxPopulationsPerEra (a cost rail — each target is a paid gpt-image-2 call). A portrait
            // is generated for EVERY population, not just the era's top source.
            var targets = eras.SelectMany(era => era.QpadmResultPopulations
                    .Where(p => p.Population is not null)
                    .GroupBy(p => p.PopulationId).Select(g => g.First()) // de-dup a population within an era (defensive)
                    .OrderByDescending(p => p.Percentage)
                    .Take(settings.MaxPopulationsPerEra)
                    .Select(p => (Era: era, Pop: p)))
                .ToList();

            var now = DateTime.UtcNow;
            var produced = 0;
            string? lastError = null;
            long inputTokens = 0, outputTokens = 0, totalTokens = 0;
            var imageCount = 0;

            // The client's gender → feminine vs masculine clothing/presentation in every portrait.
            var gender = order.GeneticInspection.Gender;

            foreach (var (era, pop) in targets)
            {
                var population = pop.Population!;
                var prompt = AncestralPortraitPrompts.Build(
                    population.Name, population.Description, era.Era?.Name, population.ImagePrompt, gender);

                try
                {
                    logger.LogInformation("Ancestral portraits set {SetId}: calling gpt-image-2 (edit) for era {EraId} population {PopulationId} as {Population}, {N} variation(s), size {Size}, quality {Quality}.",
                        set.Id, era.EraId, pop.PopulationId, population.Name, parameters.N, parameters.Size, parameters.Quality);
                    // NOTE: do NOT pass `input_fidelity` — gpt-image-2 rejects that parameter (it's a gpt-image-1
                    // edit option). The user's face photos are the reference; identity preservation is driven by the
                    // prompt's "the SAME person" lead instead.
                    var result = await openAiClient.EditAsync(
                        new OpenAIEditRequest(prompt, parameters, faceRefs, null, null), cancellationToken);

                    for (var i = 0; i < result.Images.Count; i++)
                    {
                        var bytes = result.Images[i].Bytes;
                        var key = $"users/{identitySlug}/ancestral-portraits/{set.Id:N}/{era.EraId}-{pop.PopulationId}-{i}.{ext}";
                        using (var stream = new MemoryStream(bytes, writable: false))
                            await r2Storage.UploadAsync(key, stream, contentType, cancellationToken);
                        uploadedKeys.Add(key);

                        dbContext.AncestralPortraits.Add(new AncestralPortrait
                        {
                            SetId = set.Id,
                            EraId = era.EraId,
                            EraName = era.Era?.Name ?? string.Empty,
                            PopulationId = pop.PopulationId,
                            PopulationName = population.Name,
                            R2Key = key,
                            ContentType = contentType,
                            ByteSize = bytes.LongLength,
                            VariationIndex = i,
                            IsSelected = i == 0, // default-select the first variation of each population so the share set + reel always have a pick
                            CreatedBy = user.IdentityId,
                            CreatedAt = now,
                            UpdatedAt = now,
                        });
                    }
                    produced++;
                    inputTokens += result.Usage?.InputTokens ?? 0;
                    outputTokens += result.Usage?.OutputTokens ?? 0;
                    totalTokens += result.Usage?.TotalTokens ?? 0;
                    imageCount += result.Images.Count;
                }
                catch (OpenAIImageException ex)
                {
                    lastError = ex.Detail;
                    logger.LogWarning(ex, "Ancestral portrait era {EraId} population {PopulationId} failed for set {SetId}.", era.EraId, pop.PopulationId, set.Id);
                }
            }

            set.Status = produced > 0 ? AncestralPortraitStatus.Succeeded : AncestralPortraitStatus.Failed;
            set.Error = produced > 0 ? (produced < targets.Count ? lastError : null) : (lastError ?? "Portrait generation failed.");
            set.UsageInputTokens = inputTokens;
            set.UsageOutputTokens = outputTokens;
            set.UsageTotalTokens = totalTokens;
            set.ImageCount = imageCount;
            set.EstimatedCostUsd = (inputTokens / 1_000_000m) * settings.CostPerMillionInputTokensUsd
                                 + (outputTokens / 1_000_000m) * settings.CostPerMillionOutputTokensUsd;
            set.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Ancestral portrait set {SetId} finished: {Produced}/{Total} population(s) across {Eras} era(s), {Images} image(s), {TotalTokens} tokens (in {In}/out {Out}), est. ${Cost}.",
                set.Id, produced, targets.Count, eras.Count, imageCount, totalTokens, inputTokens, outputTokens, set.EstimatedCostUsd);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ancestral portrait set {SetId} failed unexpectedly.", set.Id);
            // The final SaveChanges is all-or-nothing, so on any throw NONE of this run's portrait rows committed —
            // its R2 uploads are orphaned. Delete them (private face images), then detach the poisoned tracked inserts
            // so FailAsync can persist just the Failed status (and the set stays retryable).
            foreach (var key in uploadedKeys)
            {
                try { await r2Storage.DeleteAsync(key, cancellationToken); }
                catch (Exception cleanupEx) { logger.LogWarning(cleanupEx, "Failed to delete orphaned R2 object {Key}.", key); }
            }
            foreach (var entry in dbContext.ChangeTracker.Entries<AncestralPortrait>()
                         .Where(e => e.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
            await FailAsync(set, "Portrait generation failed unexpectedly.", cancellationToken);
        }
    }

    public async Task<AncestralPortraitUsageContract.Response> GetUsageSummaryAsync(CancellationToken cancellationToken = default)
    {
        var sets = dbContext.AncestralPortraitSets.AsNoTracking();
        return new AncestralPortraitUsageContract.Response
        {
            TotalRuns = await sets.CountAsync(cancellationToken),
            SucceededRuns = await sets.CountAsync(s => s.Status == AncestralPortraitStatus.Succeeded, cancellationToken),
            TotalImages = await sets.SumAsync(s => s.ImageCount, cancellationToken),
            TotalInputTokens = await sets.SumAsync(s => s.UsageInputTokens ?? 0, cancellationToken),
            TotalOutputTokens = await sets.SumAsync(s => s.UsageOutputTokens ?? 0, cancellationToken),
            TotalTokens = await sets.SumAsync(s => s.UsageTotalTokens ?? 0, cancellationToken),
            TotalEstimatedCostUsd = await sets.SumAsync(s => s.EstimatedCostUsd ?? 0m, cancellationToken),
        };
    }

    // MARK: helpers

    private async Task FailAsync(AncestralPortraitSet set, string error, CancellationToken cancellationToken)
    {
        set.Status = AncestralPortraitStatus.Failed;
        set.Error = error;
        set.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<AncestralPortraitSet?> LoadSetByIdAsync(Guid setId, CancellationToken cancellationToken) =>
        dbContext.AncestralPortraitSets.AsNoTracking().Include(s => s.Portraits)
            .FirstOrDefaultAsync(s => s.Id == setId, cancellationToken);

    private static AncestralPortraitSetContract.Response Empty(int orderId) => new()
    {
        OrderId = orderId,
        Status = "NotPurchased",
    };

    private AncestralPortraitSetContract.Response MapSet(AncestralPortraitSet set, int orderId) => new()
    {
        SetId = set.Id,
        OrderId = orderId,
        Status = set.Status.ToString(),
        Error = set.Error,
        CreatedAt = set.CreatedAt,
        // NOTE: cost/usage is intentionally NOT mapped here — it must never reach the iOS app/user. It's persisted on
        // the entity + the completion log + the AdminOnly `/admin/ancestral-portraits/usage` endpoint (web only).
        // One group per (era, population). Order by era, then by ancestry-rank within the era (preserved as the
        // generation/insertion order → the group's lowest portrait Id), so the story/share set reads top-source-first.
        Eras = set.Portraits
            .GroupBy(p => new { p.EraId, p.PopulationId })
            .OrderBy(g => g.Key.EraId).ThenBy(g => g.Min(p => p.Id))
            .Select(g => new AncestralPortraitEraContract.Response
            {
                EraId = g.Key.EraId,
                EraName = g.First().EraName,
                PopulationId = g.Key.PopulationId,
                PopulationName = g.First().PopulationName,
                // Defensive de-dup: collapse any accidental duplicate variations (same VariationIndex) so a portrait
                // never appears twice in a group, even if older data was generated before the anti-double-run claim.
                Portraits = g.GroupBy(p => p.VariationIndex).Select(v => v.OrderBy(p => p.Id).First())
                    .OrderBy(p => p.VariationIndex).Select(p => new AncestralPortraitContract.Response
                    {
                        Id = p.Id,
                        VariationIndex = p.VariationIndex,
                        IsSelected = p.IsSelected,
                        DownloadUrl = $"/v1/api/ancestral-portraits/{p.Id}/download",
                    }).ToList(),
            })
            .ToList(),
    };

    // Centralised so the orphan-cleanup sweep derives the same slug — see UserStorageKeys.
    private static string SanitizeIdentity(string identityId) => UserStorageKeys.Sanitize(identityId);

    /// <summary>Maps the admin-configured output format to the R2 object's content type + file extension.</summary>
    private static (string ContentType, string Ext) ResolveContentType(string outputFormat) => outputFormat switch
    {
        "png" => ("image/png", "png"),
        "webp" => ("image/webp", "webp"),
        _ => ("image/jpeg", "jpg"),
    };
}
