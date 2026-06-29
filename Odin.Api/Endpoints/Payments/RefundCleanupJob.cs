using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Odin.Api.Data;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.MergeManagement;
using Odin.Api.Endpoints.OrderManagement;
using Odin.Api.Hubs;
using Odin.Api.Storage;

namespace Odin.Api.Endpoints.Payments;

/// <summary>
/// Purges a paid qpAdm/G25 order and EVERYTHING generated from it after Apple refunds (or revokes) the purchase:
/// the order + its genetic inspection, results, clade, regions (EF cascade), the per-order add-ons (Y-DNA unlock
/// and "Through the Ages" AI-portrait sets incl. their private R2 images), and — only when no other order still
/// references it — the uploaded raw DNA file + its tools-api merge bundle. The <c>app_store_transactions</c> row is
/// deliberately KEPT (marked <c>Refunded</c> by the webhook) as the financial audit record.
/// </summary>
/// <remarks>
/// Runs as a background job so the App Store webhook stays fast. It is <b>idempotent</b> (a second run finds nothing
/// and no-ops) and <b>best-effort per asset</b> (one R2/tools-api failure is logged and the rest still runs). The
/// <c>[Queue]</c>/<c>[AutomaticRetry]</c> attributes MUST live on this <b>interface</b> method (Hangfire reads filter
/// attributes off the enqueued interface — same load-bearing rule as <c>IMergeJob</c>/<c>IAncestralPortraitWorker</c>).
/// <c>Attempts = 0</c>: a failed purge leaves recoverable orphans the weekly merge/R2 sweeps reclaim; a blanket retry
/// of a destructive job is riskier than letting an admin re-trigger it.
/// </remarks>
public interface IRefundCleanupJob
{
    [Queue("default")]
    [AutomaticRetry(Attempts = 0)]
    Task PurgeRefundedOrderAsync(ServiceType service, int orderId, CancellationToken cancellationToken = default);
}

public sealed class RefundCleanupJob(
    ApplicationDbContext dbContext,
    IMergeJob mergeJob,
    IR2Storage r2Storage,
    IGeneticInspectionRealtimeNotifier liveUpdates,
    IMemoryCache cache,
    ILogger<RefundCleanupJob> logger) : IRefundCleanupJob
{
    public async Task PurgeRefundedOrderAsync(ServiceType service, int orderId, CancellationToken cancellationToken = default)
    {
        // 1. Add-on: "Through the Ages" AI-portrait sets for this order. Delete the PRIVATE R2 images (the user's face)
        //    BEFORE the rows, so refunded biometric data never lingers in storage; the rows cascade their portraits.
        var portraitSets = await dbContext.AncestralPortraitSets
            .Include(s => s.Portraits)
            .Where(s => s.OrderId == orderId)
            .ToListAsync(cancellationToken);
        var deletedImages = 0;
        foreach (var portrait in portraitSets.SelectMany(s => s.Portraits))
        {
            try
            {
                await r2Storage.DeleteAsync(portrait.R2Key, cancellationToken);
                deletedImages++;
            }
            catch (Exception ex)
            {
                // A storage hiccup must not abort the purge — the daily R2 orphan sweep reclaims any leftover.
                logger.LogWarning(ex, "Refund purge: failed to delete portrait R2 object {Key} for order {OrderId}.", portrait.R2Key, orderId);
            }
        }
        if (portraitSets.Count > 0)
            dbContext.AncestralPortraitSets.RemoveRange(portraitSets); // cascade-deletes the AncestralPortrait rows

        // 2. Add-on: Y-DNA results unlock(s) for this order (qpAdm-only; a no-op for G25).
        var ydnaUnlocks = await dbContext.QpadmYDnaUnlocks
            .Where(u => u.OrderId == orderId)
            .ToListAsync(cancellationToken);
        if (ydnaUnlocks.Count > 0)
            dbContext.QpadmYDnaUnlocks.RemoveRange(ydnaUnlocks);

        // 3. The order itself → cascade-deletes its genetic inspection + results + clade + regions (+ profile-picture
        //    blob). Capture the raw-file id first (the inspection is about to be gone), and decide whether the file is
        //    shared by ANOTHER order BEFORE deleting this one — see the guarded cleanup note in step 4.
        int? rawGeneticFileId = null;
        if (service == ServiceType.g25)
        {
            var order = await dbContext.G25Orders
                .Include(o => o.GeneticInspection)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
            if (order is not null)
                rawGeneticFileId = order.GeneticInspection?.RawGeneticFileId;

            var fileSharedWithOtherOrder = await IsFileSharedWithOtherOrderAsync(rawGeneticFileId, service, orderId, cancellationToken);

            if (order is not null)
                dbContext.G25Orders.Remove(order);
            cache.Remove(OrderResultCacheKeys.G25(orderId));
            await dbContext.SaveChangesAsync(cancellationToken);

            await CleanupRawFileIfOrphanedAsync(rawGeneticFileId, fileSharedWithOtherOrder, orderId, cancellationToken);
        }
        else
        {
            var order = await dbContext.QpadmOrders
                .Include(o => o.GeneticInspection)
                .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);
            if (order is not null)
                rawGeneticFileId = order.GeneticInspection?.RawGeneticFileId;

            var fileSharedWithOtherOrder = await IsFileSharedWithOtherOrderAsync(rawGeneticFileId, service, orderId, cancellationToken);

            if (order is not null)
                dbContext.QpadmOrders.Remove(order);
            cache.Remove(OrderResultCacheKeys.Qpadm(orderId));
            await dbContext.SaveChangesAsync(cancellationToken);

            await CleanupRawFileIfOrphanedAsync(rawGeneticFileId, fileSharedWithOtherOrder, orderId, cancellationToken);
        }

        // Refresh the admin "Clients Ancient Origins Results" table live (the row + its inspection are gone).
        await liveUpdates.NotifyChangedAsync("Deleted");

        logger.LogInformation(
            "Refund purge complete for {Service} order {OrderId}: {Sets} AI-portrait set(s) ({Images} image(s)), {Ydna} Y-DNA unlock(s) removed.",
            service, orderId, portraitSets.Count, deletedImages, ydnaUnlocks.Count);
    }

    /// <summary>
    /// Is the raw file referenced by ANOTHER order's inspection (so it must be kept)? Evaluated BEFORE this order is
    /// deleted — excluding only THIS order's own inspection — so a concurrent refund of a sibling order can't make a
    /// file look unshared mid-decision (the prior "count after delete" was racy). The same-service side excludes
    /// <paramref name="orderId"/>; the other-service side does NOT (qpAdm and G25 order ids are independent sequences,
    /// so a same-numbered order of the other service is a genuine, different reference). A file shared with any order
    /// that still has a live inspection is therefore never wrongly deleted; the only residue is a harmless orphan when
    /// every sharer is refunded at once, which the storage sweeps reclaim.
    /// </summary>
    private async Task<bool> IsFileSharedWithOtherOrderAsync(
        int? rawGeneticFileId, ServiceType service, int orderId, CancellationToken cancellationToken)
    {
        if (rawGeneticFileId is not int fileId)
            return false;

        if (service == ServiceType.g25)
        {
            return await dbContext.G25GeneticInspections.AnyAsync(gi => gi.RawGeneticFileId == fileId && gi.OrderId != orderId, cancellationToken)
                || await dbContext.QpadmGeneticInspections.AnyAsync(gi => gi.RawGeneticFileId == fileId, cancellationToken);
        }

        return await dbContext.QpadmGeneticInspections.AnyAsync(gi => gi.RawGeneticFileId == fileId && gi.OrderId != orderId, cancellationToken)
            || await dbContext.G25GeneticInspections.AnyAsync(gi => gi.RawGeneticFileId == fileId, cancellationToken);
    }

    /// <summary>
    /// Soft-delete the now-orphaned raw DNA file + drop its tools-api merge bundle. Order matters: the file row is
    /// soft-deleted (committed) FIRST, then the bundle is dropped — so a bundle-delete failure leaves a reclaimable
    /// orphan rather than a "live" file row pointing at a bundle that's already gone. No-op when the file is shared
    /// or already soft-deleted (idempotent). The merge-bundle delete is best-effort (the weekly sweep is the backstop).
    /// </summary>
    private async Task CleanupRawFileIfOrphanedAsync(
        int? rawGeneticFileId, bool fileSharedWithOtherOrder, int orderId, CancellationToken cancellationToken)
    {
        if (rawGeneticFileId is not int fileId || fileSharedWithOtherOrder)
            return;

        // Soft-delete the file row first (the repo's model — the global `!IsDeleted` filter then hides it; a HARD
        // delete would cascade-delete any inspection still pointing at it, but we've established there is none).
        var file = await dbContext.RawGeneticFiles
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(f => f.Id == fileId && !f.IsDeleted, cancellationToken);
        if (file is not null)
        {
            file.IsDeleted = true;
            file.UpdatedAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        // Then drop the tools-api merge bundle (best-effort — IgnoreQueryFilters inside, so the soft-delete above
        // doesn't hide the file from it; a no-op when the file never produced a bundle).
        try
        {
            await mergeJob.DeleteAsync(fileId, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Refund purge: failed to delete merge bundle for raw file {FileId} (order {OrderId}).", fileId, orderId);
        }
    }
}
