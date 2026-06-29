using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;

namespace Odin.Api.Hubs;

/// <summary>
/// Pushes a live "an App Store purchase changed" signal so the admin "App Store Transactions" page updates
/// without polling — a new paid order or add-on lands, or a refund is reconciled. Targets the
/// <see cref="NotificationHub.AdminGroup"/> (admins only) under one event name, <c>AppStorePurchasesChanged</c>;
/// the small payload (which carries customer/amount detail) must NOT reach non-admin clients, so this is a
/// group send, not a <c>Clients.All</c> broadcast. The payload lets the page both refetch and show a toast.
/// </summary>
public interface IAppStorePurchaseRealtimeNotifier
{
    /// <summary>Broadcast that a new purchase was recorded (paid order or add-on), with enough detail for a toast.</summary>
    Task NotifyPurchaseRecordedAsync(
        string kind,
        string productLabel,
        decimal amount,
        string currency,
        string? createdBySub,
        CancellationToken cancellationToken = default);

    /// <summary>Broadcast that a purchase was refunded/revoked (so the page re-flags it live).</summary>
    Task NotifyRefundedAsync(string transactionId, CancellationToken cancellationToken = default);
}

public sealed class AppStorePurchaseRealtimeNotifier(
    IHubContext<NotificationHub> hubContext,
    ApplicationDbContext dbContext,
    ILogger<AppStorePurchaseRealtimeNotifier> logger) : IAppStorePurchaseRealtimeNotifier
{
    private const string EventName = "AppStorePurchasesChanged";

    public async Task NotifyPurchaseRecordedAsync(
        string kind,
        string productLabel,
        decimal amount,
        string currency,
        string? createdBySub,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve a friendly customer label from the Auth0 sub (best-effort — the toast still reads fine without it).
            string? customer = null;
            if (!string.IsNullOrWhiteSpace(createdBySub))
            {
                var owner = await dbContext.Users.AsNoTracking()
                    .Where(u => u.IdentityId == createdBySub)
                    .Select(u => new { u.Email, u.FirstName, u.LastName })
                    .FirstOrDefaultAsync(cancellationToken);
                if (owner is not null)
                {
                    customer = !string.IsNullOrWhiteSpace(owner.Email)
                        ? owner.Email
                        : $"{owner.FirstName} {owner.LastName}".Trim();
                    if (string.IsNullOrWhiteSpace(customer))
                        customer = null;
                }
            }

            await hubContext.Clients.Group(NotificationHub.AdminGroup).SendAsync(
                EventName,
                new { kind, productLabel, amount, currency, customer, transactionId = (string?)null },
                cancellationToken);
        }
        catch (Exception ex)
        {
            // A failed live-refresh push must never fail the purchase that triggered it; the page still
            // catches up on its next load. Log and move on.
            logger.LogWarning(ex, "Failed to push App Store purchase-recorded signal ({Kind}).", kind);
        }
    }

    public async Task NotifyRefundedAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        try
        {
            await hubContext.Clients.Group(NotificationHub.AdminGroup).SendAsync(
                EventName,
                new
                {
                    kind = "Refund",
                    productLabel = (string?)null,
                    amount = (decimal?)null,
                    currency = (string?)null,
                    customer = (string?)null,
                    transactionId,
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to push App Store refund signal for {TransactionId}.", transactionId);
        }
    }
}
