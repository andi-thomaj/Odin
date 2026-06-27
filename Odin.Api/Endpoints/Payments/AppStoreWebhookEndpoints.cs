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
                // Match by Apple's globally-unique transaction id.
                var txn = await dbContext.AppStoreTransactions
                    .FirstOrDefaultAsync(t => t.TransactionId == transactionId, ct);

                if (txn is not null && txn.Status != AppStoreTransactionStatus.Refunded)
                {
                    txn.Status = AppStoreTransactionStatus.Refunded;
                    txn.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(ct);
                    logger.LogInformation(
                        "App Store transaction {TransactionId} marked Refunded ({NotificationType}).",
                        transactionId, notification.NotificationType);
                }
            }

            // Always 200 for anything we successfully verified — otherwise Apple retries for hours on events
            // we intentionally don't act on.
            return Results.Ok();
        }

        /// <summary>Marker type so the endpoint can resolve a category-named <see cref="ILogger{T}"/>.</summary>
        private sealed class AppStoreWebhookMarker;
    }
}
