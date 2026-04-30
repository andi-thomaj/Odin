using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Endpoints.PaddleAdminManagement.Models;
using Odin.Api.Services.Paddle;
using Odin.Api.Services.Paddle.Resources;
using Odin.Api.Services.Paddle.Sync;

namespace Odin.Api.Endpoints.PaddleAdminManagement;

/// <summary>
/// Admin-only endpoints for inspecting and operating on Paddle data: trigger sync runs,
/// inspect the notification log, and ask Paddle to replay a notification.
/// </summary>
public static class PaddleAdminEndpoints
{
    public static void MapPaddleAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/admin/paddle")
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict")
            .WithTags("Paddle Admin");

        group.MapPost("/sync/products", SyncProducts).WithSummary("Pull all products + prices from Paddle and upsert.");
        group.MapPost("/sync/products/{productId}", SyncProduct).WithSummary("Re-sync a single product.");
        group.MapPost("/sync/customers", SyncCustomers).WithSummary("Pull all customers from Paddle and upsert.");
        group.MapPost("/sync/customers/{customerId}", SyncCustomer).WithSummary("Re-sync a single customer.");
        group.MapPost("/sync/subscriptions", SyncSubscriptions).WithSummary("Pull all subscriptions from Paddle and upsert.");
        group.MapPost("/sync/subscriptions/{subscriptionId}", SyncSubscription).WithSummary("Re-sync a single subscription.");
        group.MapPost("/sync/transactions", SyncTransactions).WithSummary("Pull transactions billed at/after the supplied cutoff.");
        group.MapPost("/sync/transactions/{transactionId}", SyncTransaction).WithSummary("Re-sync a single transaction.");

        group.MapGet("/notifications", ListNotifications).WithSummary("Local notification log (last 100, newest first).");
        group.MapGet("/notifications/{id:int}", GetNotification).WithSummary("Local notification log row, including raw payload.");
        group.MapPost("/notifications/{id:int}/replay", ReplayNotification)
            .WithSummary("Asks Paddle to replay this notification. Only event-origin notifications under 90 days old are eligible.");
        group.MapPost("/notifications/backfill", BackfillNotifications)
            .WithSummary("Pull recent notifications from Paddle and store any we don't already have locally.");
    }

    private static async Task<IResult> SyncProducts(IPaddleProductSyncService sync, CancellationToken ct)
        => Results.Ok(ToResponse(await sync.SyncAllAsync(ct)));

    private static async Task<IResult> SyncProduct(string productId, IPaddleProductSyncService sync, CancellationToken ct)
        => Results.Ok(ToResponse(await sync.SyncOneAsync(productId, ct)));

    private static async Task<IResult> SyncCustomers(IPaddleCustomerSyncService sync, CancellationToken ct)
        => Results.Ok(ToResponse(await sync.SyncAllAsync(ct)));

    private static async Task<IResult> SyncCustomer(string customerId, IPaddleCustomerSyncService sync, CancellationToken ct)
        => Results.Ok(ToResponse(await sync.SyncOneAsync(customerId, ct)));

    private static async Task<IResult> SyncSubscriptions(IPaddleSubscriptionSyncService sync, CancellationToken ct)
        => Results.Ok(ToResponse(await sync.SyncAllAsync(ct)));

    private static async Task<IResult> SyncSubscription(string subscriptionId, IPaddleSubscriptionSyncService sync, CancellationToken ct)
        => Results.Ok(ToResponse(await sync.SyncOneAsync(subscriptionId, ct)));

    private static async Task<IResult> SyncTransactions(
        TransactionSyncRequest? request,
        IPaddleTransactionSyncService sync,
        CancellationToken ct)
        => Results.Ok(ToResponse(await sync.SyncAllAsync(request?.BilledAtFrom, ct)));

    private static async Task<IResult> SyncTransaction(string transactionId, IPaddleTransactionSyncService sync, CancellationToken ct)
        => Results.Ok(ToResponse(await sync.SyncOneAsync(transactionId, ct)));

    private static async Task<IResult> ListNotifications(ApplicationDbContext db, CancellationToken ct)
    {
        var rows = await db.PaddleNotifications
            .AsNoTracking()
            .OrderByDescending(n => n.ReceivedAt)
            .Take(100)
            .Select(n => new PaddleNotificationListItem(
                n.Id, n.PaddleNotificationId, n.EventType, n.PaddleEventId, n.Origin,
                n.OccurredAt, n.ReceivedAt, n.ProcessedAt, n.ProcessedStatus,
                n.ProcessingAttempts, n.ProcessingError, n.Source))
            .ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> GetNotification(int id, ApplicationDbContext db, CancellationToken ct)
    {
        var n = await db.PaddleNotifications.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return n is null
            ? Results.NotFound()
            : Results.Ok(new PaddleNotificationDetail(
                n.Id, n.PaddleNotificationId, n.EventType, n.PaddleEventId, n.Origin,
                n.OccurredAt, n.ReceivedAt, n.ProcessedAt, n.ProcessedStatus,
                n.ProcessingAttempts, n.ProcessingError, n.Source, n.Payload));
    }

    private static async Task<IResult> ReplayNotification(
        int id,
        ApplicationDbContext db,
        IPaddleNotificationStore store,
        ILoggerFactory loggerFactory,
        CancellationToken ct)
    {
        var logger = loggerFactory.CreateLogger("Odin.Api.PaddleAdmin.Replay");
        var notification = await db.PaddleNotifications
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == id, ct);

        if (notification is null)
            return Results.NotFound();

        try
        {
            var newId = await store.ReplayWithPaddleAsync(notification.PaddleNotificationId, ct);
            return Results.Ok(new ReplayNotificationResponse(newId));
        }
        catch (PaddleApiException ex)
        {
            logger.LogWarning(ex, "Paddle replay rejected for {NotificationId}: {Status} {Code}.",
                notification.PaddleNotificationId, ex.StatusCode, ex.PaddleErrorCode);
            return Results.Problem(
                title: "Paddle rejected the replay request.",
                detail: ex.Message,
                statusCode: (int)ex.StatusCode);
        }
    }

    private static async Task<IResult> BackfillNotifications(
        BackfillNotificationsRequest? request,
        IPaddleNotificationStore store,
        CancellationToken ct)
    {
        var query = new PaddleNotificationListQuery
        {
            From = request?.From,
            To = request?.To,
            Status = request?.Status,
            PerPage = 200,
        };
        var result = await store.BackfillFromPaddleAsync(query, ct);
        return Results.Ok(ToResponse(result));
    }

    private static PaddleSyncResultResponse ToResponse(PaddleSyncResult r) =>
        new(r.Resource, r.Inserted, r.Updated, r.Skipped, r.Failed, r.Total, r.Errors);
}
