using Hangfire;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.Payments.Models;

namespace Odin.Api.Endpoints.Payments
{
    /// <summary>
    /// App Store Server Notifications V2 receiver. Apple POSTs signed lifecycle events here (configure the
    /// URL in App Store Connect). We act on <c>REFUND</c> / <c>REVOKE</c> by marking the matching
    /// <see cref="Data.Entities.AppStoreTransaction"/> as <see cref="AppStoreTransactionStatus.Refunded"/>,
    /// so refunds aren't invisible server-side. Anonymous (Apple sends no bearer); authenticity comes from
    /// the JWS signature, verified by <see cref="IAppStorePurchaseService.ParseNotification"/>.
    /// </summary>
    public static class AppStoreWebhookEndpoints
    {
        public static void MapAppStoreWebhookEndpoints(this IEndpointRouteBuilder app)
        {
            var group = app.MapGroup("api/webhooks/app-store")
                .AllowAnonymous()
                .RequireRateLimiting("strict")
                .WithTags("AppStoreWebhook");

            group.MapPost("/", Handle)
                .WithSummary("Receives Apple App Store Server Notifications V2 (refund/revoke handling).")
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest);
        }

        private static async Task<IResult> Handle(
            AppStoreNotificationRequest request,
            IAppStorePurchaseService purchase,
            ApplicationDbContext dbContext,
            Odin.Api.Hubs.IAppStorePurchaseRealtimeNotifier purchaseLiveUpdates,
            IBackgroundJobClient backgroundJobs,
            ILogger<AppStoreWebhookMarker> logger,
            CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(request?.SignedPayload))
                return Results.BadRequest(new { error = "signedPayload is required." });

            AppStoreNotification notification;
            try
            {
                notification = purchase.ParseNotification(request.SignedPayload);
            }
            catch (AppStorePurchaseException ex)
            {
                // Reject unverifiable payloads (a 400 tells Apple it wasn't accepted; genuine Apple
                // notifications always verify). Never leak internals.
                logger.LogWarning(ex, "Rejected an unverifiable App Store notification.");
                return Results.BadRequest(new { error = "Invalid notification payload." });
            }

            // Only refunds / revocations change our state today. CONSUMPTION_REQUEST, RENEWAL, etc. are
            // acknowledged (200) but ignored — consumables don't renew.
            if (notification.TransactionId is { Length: > 0 } transactionId
                && notification.NotificationType is "REFUND" or "REVOKE")
            {
                var changed = false;

                // Match by Apple's globally-unique transaction id.
                var txn = await dbContext.AppStoreTransactions
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId, ct);

                if (txn is not null && txn.Status != AppStoreTransactionStatus.Refunded)
                {
                    txn.Status = AppStoreTransactionStatus.Refunded;
                    txn.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(ct);
                    changed = true;
                    logger.LogInformation(
                        "App Store transaction {TransactionId} marked Refunded ({NotificationType}).",
                        transactionId, notification.NotificationType);

                    // A refunded ORDER purchase loses its paid results: enqueue a background purge of the order + its
                    // generated results + add-ons (the txn row itself stays as the Refunded audit record). The add-on
                    // transactions (Y-DNA / AI-portraits) are NOT order rows — they carry no QpadmOrderId/G25OrderId,
                    // so they're handled by their own reconcile paths below, never here. The enqueue is best-effort:
                    // a Hangfire hiccup must NOT fail the webhook (Apple would retry for hours, and the txn is already
                    // committed Refunded). A failed enqueue leaves the order reclaimable via an admin re-trigger / the
                    // weekly orphan sweeps.
                    try
                    {
                        if (txn.QpadmOrderId is int qpadmOrderId)
                            backgroundJobs.Enqueue<IRefundCleanupJob>(
                                j => j.PurgeRefundedOrderAsync(ServiceType.qpAdm, qpadmOrderId, CancellationToken.None));
                        else if (txn.G25OrderId is int g25OrderId)
                            backgroundJobs.Enqueue<IRefundCleanupJob>(
                                j => j.PurgeRefundedOrderAsync(ServiceType.g25, g25OrderId, CancellationToken.None));
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex,
                            "Failed to enqueue refund purge for transaction {TransactionId}; the order remains and must be reclaimed manually / by the orphan sweeps.",
                            transactionId);
                    }
                }

                // The Y-DNA results unlock is a separate per-order entitlement (its own `qpadm_ydna_unlocks` table,
                // not `app_store_transactions`), so revoke it explicitly on refund: deleting the row re-locks the
                // clade (the per-request gate reads the table live, so the next result view is locked again).
                var ydnaUnlock = await dbContext.QpadmYDnaUnlocks
                    .FirstOrDefaultAsync(u => u.TransactionId == transactionId, ct);
                if (ydnaUnlock is not null)
                {
                    dbContext.QpadmYDnaUnlocks.Remove(ydnaUnlock);
                    await dbContext.SaveChangesAsync(ct);
                    changed = true;
                    logger.LogInformation(
                        "Y-DNA unlock for order {OrderId} revoked ({NotificationType}, txn {TransactionId}).",
                        ydnaUnlock.OrderId, notification.NotificationType, transactionId);
                }

                // The "Through the Ages" AI-portraits add-on lives in its own table too. Unlike the Y-DNA unlock we
                // do NOT delete it (that would destroy the user's generated portraits) — we stamp RefundedAt so the
                // back-office feed shows it as refunded and drops it from revenue, while the images are preserved.
                var portraitSet = await dbContext.AncestralPortraitSets
                    .FirstOrDefaultAsync(s => s.TransactionId == transactionId, ct);
                if (portraitSet is not null && portraitSet.RefundedAt is null)
                {
                    portraitSet.RefundedAt = DateTime.UtcNow;
                    portraitSet.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(ct);
                    changed = true;
                    logger.LogInformation(
                        "AI-portraits set {SetId} marked refunded ({NotificationType}, txn {TransactionId}).",
                        portraitSet.Id, notification.NotificationType, transactionId);
                }

                // Live-push the refund to the admin "App Store Transactions" page so the row re-flags / drops
                // without a manual reload (best-effort; the notifier swallows its own failures).
                if (changed)
                    await purchaseLiveUpdates.NotifyRefundedAsync(transactionId, ct);
            }

            // Always 200 for anything we successfully verified — otherwise Apple retries for hours on events
            // we intentionally don't act on.
            return Results.Ok();
        }

        /// <summary>Marker type so the endpoint can resolve a category-named <see cref="ILogger{T}"/>.</summary>
        private sealed class AppStoreWebhookMarker;
    }
}
