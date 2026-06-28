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
    IImageSettingsService settingsService,
    IR2Storage r2Storage,
    IAppStorePurchaseService appStore,
    IBackgroundJobClient backgroundJobs,
    IOptions<AppleIapOptions> appleOptions,
    IOptions<AncestralPortraitLimitsOptions> limitsOptions,
    ILogger<AncestralPortraitService> logger) : IAncestralPortraitService
{
    private readonly AppleIapOptions _apple = appleOptions.Value;
    private readonly AncestralPortraitLimitsOptions _limits = limitsOptions.Value;

    public async Task<(AncestralPortraitSetContract.Response Response, int StatusCode, string? Error)> PurchaseAsync(
        int orderId, string identityId, string transactionJws, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.QpadmOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
            return (Empty(orderId), 404, $"Order {orderId} not found.");
        if (order.CreatedBy != identityId)
            return (Empty(orderId), 403, "You do not own this order.");

        var user = await dbContext.Users.RequireByIdentityAsync(identityId, cancellationToken);

        // Already unlocked for this order ⇒ idempotent (return the existing set; don't re-charge/re-create).
        var existing = await LoadSetAsync(orderId, user.Id, cancellationToken);
        if (existing is not null)
            return (MapSet(existing, orderId), 200, null);

        // Validate the StoreKit add-on purchase (throws AppStorePurchaseException → 400 at the endpoint).
        var verified = appStore.ValidateAddOnTransaction(transactionJws, _apple.AiPortraitsProductId);

        // Replay of the SAME transaction (e.g. a retried request) ⇒ return whatever set it created.
        var byTxn = await dbContext.AncestralPortraitSets
            .FirstOrDefaultAsync(s => s.TransactionId == verified.TransactionId, cancellationToken);
        if (byTxn is not null)
        {
            var reloaded = await LoadSetAsync(byTxn.OrderId, byTxn.UserId, cancellationToken);
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
        await dbContext.SaveChangesAsync(cancellationToken);

        backgroundJobs.Enqueue<IAncestralPortraitWorker>(w => w.RunAsync(set.Id, CancellationToken.None));

        return (MapSet(set, orderId), 201, null);
    }

    public async Task<(AncestralPortraitSetContract.Response? Response, int StatusCode)> GetSetAsync(
        int orderId, string identityId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.QpadmOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
            return (null, 404);
        if (order.CreatedBy != identityId)
            return (null, 403);

        var user = await dbContext.Users.RequireByIdentityAsync(identityId, cancellationToken);
        var set = await LoadSetAsync(orderId, user.Id, cancellationToken);
        return (set is null ? Empty(orderId) : MapSet(set, orderId), 200);
    }

    public async Task<int> RequestGenerateAsync(int orderId, string identityId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.QpadmOrders.AsNoTracking().FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null) return 404;
        if (order.CreatedBy != identityId) return 403;

        var user = await dbContext.Users.RequireByIdentityAsync(identityId, cancellationToken);
        var set = await dbContext.AncestralPortraitSets
            .FirstOrDefaultAsync(s => s.OrderId == orderId && s.UserId == user.Id, cancellationToken);
        if (set is null) return 404; // not purchased

        backgroundJobs.Enqueue<IAncestralPortraitWorker>(w => w.RunAsync(set.Id, CancellationToken.None));
        return 202;
    }

    public async Task<int> SelectAsync(int portraitId, string identityId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.RequireByIdentityAsync(identityId, cancellationToken);
        var portrait = await dbContext.AncestralPortraits.Include(p => p.Set)
            .FirstOrDefaultAsync(p => p.Id == portraitId, cancellationToken);
        if (portrait is null) return 404;
        if (portrait.Set.UserId != user.Id) return 403;

        var siblings = await dbContext.AncestralPortraits
            .Where(p => p.SetId == portrait.SetId && p.EraId == portrait.EraId)
            .ToListAsync(cancellationToken);
        foreach (var sibling in siblings)
            sibling.IsSelected = sibling.Id == portraitId;
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
        if (set.Status == AncestralPortraitStatus.Succeeded)
            return; // idempotent

        set.Status = AncestralPortraitStatus.Running;
        set.Error = null;
        set.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        try
        {
            var user = await dbContext.Users.FirstAsync(u => u.Id == set.UserId, cancellationToken);

            // Face reference photos (the user's own) → gpt-image-2 edit inputs.
            var facePhotos = await dbContext.UserFacePhotos.AsNoTracking()
                .Where(p => p.UserId == set.UserId).OrderBy(p => p.Id)
                .Take(_limits.MaxFaceReferences).ToListAsync(cancellationToken);
            var faceRefs = new List<OpenAIReferenceImage>();
            foreach (var photo in facePhotos)
            {
                var bytes = await r2Storage.DownloadAsync(photo.R2Key, cancellationToken);
                if (bytes is not null)
                    faceRefs.Add(new OpenAIReferenceImage(bytes, photo.ContentType, photo.OriginalFileName));
            }
            if (faceRefs.Count == 0)
            {
                await FailAsync(set, "Capture your face photos first (Settings → AI Face Capture).", cancellationToken);
                return;
            }

            // The order's qpAdm eras (each era's top population becomes one portrait group).
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

            var settings = await settingsService.GetAsync(cancellationToken);
            var parameters = new OpenAIImageParameters(
                settings.Model, _limits.Size, _limits.Quality, "auto", "jpeg", null, "auto",
                _limits.VariationsPerEra, user.IdentityId);

            var identitySlug = SanitizeIdentity(user.IdentityId);
            var eras = qpadm.QpadmResultEraGroups.Take(_limits.MaxEras).ToList();
            var now = DateTime.UtcNow;
            var produced = 0;
            string? lastError = null;

            foreach (var era in eras)
            {
                var top = era.QpadmResultPopulations.OrderByDescending(p => p.Percentage).FirstOrDefault();
                if (top?.Population is null)
                    continue;

                var prompt = AncestralPortraitPrompts.Build(
                    top.Population.Name, top.Population.Description, era.Era?.Name, top.Population.ImagePrompt);

                try
                {
                    var result = await openAiClient.EditAsync(
                        new OpenAIEditRequest(prompt, parameters, faceRefs, null, "high"), cancellationToken);

                    for (var i = 0; i < result.Images.Count; i++)
                    {
                        var bytes = result.Images[i].Bytes;
                        var key = $"users/{identitySlug}/ancestral-portraits/{set.Id:N}/{era.EraId}-{i}.jpg";
                        using (var stream = new MemoryStream(bytes, writable: false))
                            await r2Storage.UploadAsync(key, stream, "image/jpeg", cancellationToken);

                        dbContext.AncestralPortraits.Add(new AncestralPortrait
                        {
                            SetId = set.Id,
                            EraId = era.EraId,
                            EraName = era.Era?.Name ?? string.Empty,
                            PopulationName = top.Population.Name,
                            R2Key = key,
                            ContentType = "image/jpeg",
                            ByteSize = bytes.LongLength,
                            VariationIndex = i,
                            IsSelected = i == 0, // default-select the first so the share set + reel always have a pick
                            CreatedBy = user.IdentityId,
                            CreatedAt = now,
                            UpdatedAt = now,
                        });
                    }
                    produced++;
                }
                catch (OpenAIImageException ex)
                {
                    lastError = ex.Detail;
                    logger.LogWarning(ex, "Ancestral portrait era {EraId} failed for set {SetId}.", era.EraId, set.Id);
                }
            }

            set.Status = produced > 0 ? AncestralPortraitStatus.Succeeded : AncestralPortraitStatus.Failed;
            set.Error = produced > 0 ? (produced < eras.Count ? lastError : null) : (lastError ?? "Portrait generation failed.");
            set.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Ancestral portrait set {SetId} finished: {Produced}/{Total} eras.", set.Id, produced, eras.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Ancestral portrait set {SetId} failed unexpectedly.", set.Id);
            await FailAsync(set, "Portrait generation failed unexpectedly.", cancellationToken);
        }
    }

    // MARK: helpers

    private async Task FailAsync(AncestralPortraitSet set, string error, CancellationToken cancellationToken)
    {
        set.Status = AncestralPortraitStatus.Failed;
        set.Error = error;
        set.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Task<AncestralPortraitSet?> LoadSetAsync(int orderId, int userId, CancellationToken cancellationToken) =>
        dbContext.AncestralPortraitSets.AsNoTracking().Include(s => s.Portraits)
            .FirstOrDefaultAsync(s => s.OrderId == orderId && s.UserId == userId, cancellationToken);

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
        Eras = set.Portraits
            .GroupBy(p => p.EraId)
            .OrderBy(g => g.Key)
            .Select(g => new AncestralPortraitEraContract.Response
            {
                EraId = g.Key,
                EraName = g.First().EraName,
                PopulationName = g.First().PopulationName,
                Portraits = g.OrderBy(p => p.VariationIndex).Select(p => new AncestralPortraitContract.Response
                {
                    Id = p.Id,
                    VariationIndex = p.VariationIndex,
                    IsSelected = p.IsSelected,
                    DownloadUrl = $"/v1/api/ancestral-portraits/{p.Id}/download",
                }).ToList(),
            })
            .ToList(),
    };

    private static string SanitizeIdentity(string identityId) =>
        new(identityId.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());
}
