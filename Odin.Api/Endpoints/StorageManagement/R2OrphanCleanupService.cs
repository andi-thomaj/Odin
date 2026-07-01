using Hangfire;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Storage;

namespace Odin.Api.Endpoints.StorageManagement;

/// <summary>Result of an orphan-cleanup pass (what was scanned + removed, or — in a dry run — what WOULD be removed).</summary>
public sealed record R2CleanupReport(
    bool DryRun,
    int ScannedUserSlugs,
    int LiveUsers,
    IReadOnlyList<string> OrphanSlugs,
    int DeletedObjects,
    bool Aborted = false,
    string? AbortReason = null);

/// <summary>
/// Reconciles per-user R2 data against the database: every <c>users/{slug}/</c> tree whose slug matches NO current
/// user (the user was deleted — their face photos / ancestral-portrait rows cascade-deleted, but the R2 objects, which
/// are private biometric images, linger) is deleted. Runs as a daily Hangfire job and on-demand via an AdminOnly
/// endpoint.
/// </summary>
public interface IR2OrphanCleanupService
{
    /// <summary>Sweep <c>users/{slug}/</c> trees with no matching DB user. <paramref name="dryRun"/> reports what
    /// WOULD be deleted without deleting anything.</summary>
    Task<R2CleanupReport> RunAsync(bool dryRun, CancellationToken cancellationToken = default);

    /// <summary>Recurring-job entry point — runs the REAL sweep (deletes orphans). The attribute is on the INTERFACE
    /// because Hangfire reads job filters from the invoked interface method, not the concrete class.</summary>
    [AutomaticRetry(Attempts = 0)] // a missed run is picked up by the next daily schedule; never want a retry storm of deletes
    Task SweepAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc cref="IR2OrphanCleanupService"/>
public sealed class R2OrphanCleanupService(
    ApplicationDbContext dbContext,
    IR2Storage r2Storage,
    ILogger<R2OrphanCleanupService> logger) : IR2OrphanCleanupService
{
    /// Below this many R2 user slugs the orphan-ratio guard isn't meaningful (a small/early dataset where deleting one
    /// user is naturally a large fraction).
    private const int RatioGuardMinSlugs = 20;
    /// Above this orphan ratio (once there are enough slugs) the sweep refuses to delete — the live set looks wrong.
    private const double MaxOrphanRatio = 0.5;

    public Task SweepAsync(CancellationToken cancellationToken = default) => RunAsync(dryRun: false, cancellationToken);

    public async Task<R2CleanupReport> RunAsync(bool dryRun, CancellationToken cancellationToken = default)
    {
        // 1. The per-user "folders" that exist in R2 (users/{slug}/ entries under users/).
        var commonPrefixes = await r2Storage.ListCommonPrefixesAsync(UserStorageKeys.RootPrefix, "/", cancellationToken);
        var r2Slugs = commonPrefixes
            .Select(StripToSlug)
            .Where(s => s.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        // 2. The slug of every CURRENT user (the same Sanitize the uploaders used).
        var identityIds = await dbContext.Users.AsNoTracking()
            .Select(u => u.IdentityId)
            .ToListAsync(cancellationToken);
        var liveSlugs = identityIds.Select(UserStorageKeys.Sanitize).ToHashSet(StringComparer.Ordinal);

        // 3. Orphans = R2 slugs that map to no live user. Sanitize is many→one, so a slug shared with a live user is
        //    KEPT — we only ever delete a slug that NO live user maps to, so a real user's data can never be removed
        //    (we err toward keeping data on the rare collision, never toward deleting a live user's).
        var orphanSlugs = r2Slugs.Where(s => !liveSlugs.Contains(s)).ToList();

        // Safety circuit-breakers against a catastrophic wipe — the failure mode is asymmetric: under-counting the R2
        // side merely deletes less (safe), but under-counting the LIVE-user side would delete real users' irreplaceable
        // biometric data. So before deleting anything, refuse on two "the DB looks wrong, not reality" signals:
        //  (1) ZERO live users but R2 HAS per-user data — almost always a wrong/empty DB connection or a failed
        //      migration, never "every user was legitimately deleted".
        //  (2) MASS orphans — more than half the R2 user trees orphaned in a single sweep (only meaningful once there's
        //      a non-trivial number of users) signals the live set is wrong (e.g. a partial restore), not real churn.
        // A logged error lets an admin investigate + fix the DB; the next run proceeds normally once it's healthy.
        var orphanRatio = r2Slugs.Count == 0 ? 0d : (double)orphanSlugs.Count / r2Slugs.Count;
        string? abortReason =
            liveSlugs.Count == 0 && r2Slugs.Count > 0
                ? $"0 live users but {r2Slugs.Count} user slug(s) in R2 (likely a DB misconfiguration)"
            : r2Slugs.Count >= RatioGuardMinSlugs && orphanRatio > MaxOrphanRatio
                ? $"{orphanSlugs.Count}/{r2Slugs.Count} slugs orphaned ({orphanRatio:P0} > {MaxOrphanRatio:P0}) — refusing a mass delete"
            : null;
        if (abortReason is not null)
            logger.LogError("R2 orphan cleanup ABORTED: {Reason}. Nothing deleted.", abortReason);

        var deleted = 0;
        if (abortReason is null)
        {
            foreach (var slug in orphanSlugs)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var keys = await r2Storage.ListKeysAsync($"{UserStorageKeys.RootPrefix}{slug}/", cancellationToken);
                    logger.LogWarning(
                        "R2 orphan cleanup: user slug '{Slug}' has no live DB user — {Count} object(s){Action}.",
                        slug, keys.Count, dryRun ? " (dry run — NOT deleted)" : " — deleting");
                    if (!dryRun && keys.Count > 0)
                        await r2Storage.DeleteManyAsync(keys, cancellationToken);
                    deleted += keys.Count;
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception ex)
                {
                    // Best-effort per slug: one slug's R2 hiccup (throttling/5xx) must not strand the rest — the next
                    // daily run retries it.
                    logger.LogWarning(ex, "R2 orphan cleanup: failed to clean slug '{Slug}' — continuing.", slug);
                }
            }
        }

        logger.LogInformation(
            "R2 orphan cleanup {Mode}: {Slugs} R2 user slug(s), {Live} live user(s), {Orphans} orphan(s), {Deleted} object(s) {Verb}.",
            dryRun ? "DRY RUN" : "complete", r2Slugs.Count, liveSlugs.Count, orphanSlugs.Count, deleted,
            abortReason is not null ? "skipped (safety abort)" : dryRun ? "would be deleted" : "deleted");

        return new R2CleanupReport(dryRun, r2Slugs.Count, liveSlugs.Count, orphanSlugs, deleted, abortReason is not null, abortReason);
    }

    /// "users/google-oauth2_123/" → "google-oauth2_123".
    private static string StripToSlug(string commonPrefix)
    {
        var s = commonPrefix.TrimEnd('/');
        return s.StartsWith(UserStorageKeys.RootPrefix, StringComparison.Ordinal)
            ? s[UserStorageKeys.RootPrefix.Length..]
            : s;
    }
}
