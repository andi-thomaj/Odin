using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Services.Paddle;
using Odin.Api.Services.Paddle.Models.Transactions;
using Odin.Api.Services.Paddle.Resources;

namespace Odin.Api.Endpoints.CheckoutManagement;

public static class CheckoutEndpoints
{
    private static readonly JsonSerializerOptions ItemsJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void MapCheckoutEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("api/checkout");

        group.MapGet("/status", GetCheckoutStatus)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated")
            .WithTags("Checkout")
            .WithSummary("Check whether the current user has an unused paid credit");

        group.MapPost("/confirm", ConfirmTransaction)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("strict")
            .WithTags("Checkout")
            .WithSummary("Verify a Paddle transaction via API and record the payment");
    }

    private static async Task<IResult> GetCheckoutStatus(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        IPaddleTransactionsResource transactionsResource,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub")
                         ?? string.Empty;

        var payment = await dbContext.PaddlePayments
            .Where(p => p.UserId == identityId && p.Status == "paid" && p.OrderId == null)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // Lazy-backfill: payments confirmed before ItemsJson existed have no snapshot, so the
        // dashboard would render "1 × qpAdm order" instead of the actual line items. Fetch from
        // Paddle once and persist; subsequent reads stay free of API calls. We also refresh
        // snapshots that exist but have all-zero unit prices (a previous bug, defensive).
        if (payment is not null && IsItemsSnapshotStale(payment.ItemsJson))
        {
            var logger = loggerFactory.CreateLogger("Odin.Api.Checkout.Status");
            try
            {
                var transaction = await transactionsResource.GetAsync(
                    payment.PaddleTransactionId, cancellationToken: cancellationToken);
                var itemsJson = await BuildItemsSnapshotAsync(dbContext, transaction, cancellationToken);
                if (!string.IsNullOrEmpty(itemsJson))
                {
                    payment.ItemsJson = itemsJson;
                    payment.UpdatedAt = DateTime.UtcNow;
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
            catch (PaddleApiException ex)
            {
                // Don't fail the status call if Paddle is unreachable — just skip the backfill;
                // the frontend will show the bare-total fallback. Try again next time.
                logger.LogWarning(ex,
                    "Paddle items backfill skipped for payment {PaymentId} (txn {TxnId}): {Status}.",
                    payment.Id, payment.PaddleTransactionId, ex.StatusCode);
            }
        }

        return Results.Ok(BuildStatusResponse(payment));
    }

    private static async Task<IResult> ConfirmTransaction(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        IPaddleTransactionsResource transactionsResource,
        ILoggerFactory loggerFactory,
        ConfirmTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Odin.Api.Checkout.Confirm");
        var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub")
                         ?? string.Empty;

        if (string.IsNullOrWhiteSpace(request.TransactionId))
            return Results.BadRequest("transactionId is required.");

        var existing = await dbContext.PaddlePayments
            .FirstOrDefaultAsync(p => p.PaddleTransactionId == request.TransactionId, cancellationToken);
        if (existing is not null)
        {
            var stillUnused = existing.UserId == identityId && existing.Status == "paid" && existing.OrderId == null;
            return Results.Ok(BuildStatusResponse(stillUnused ? existing : null));
        }

        PaddleTransactionDto transaction;
        try
        {
            transaction = await transactionsResource.GetAsync(request.TransactionId, cancellationToken: cancellationToken);
        }
        catch (PaddleApiException ex)
        {
            logger.LogWarning(ex, "Paddle returned {Status} for transaction {TxnId}.", ex.StatusCode, request.TransactionId);
            return Results.BadRequest("Could not verify this transaction with Paddle.");
        }

        if (transaction.Status is not "completed" and not "paid")
        {
            logger.LogInformation("Transaction {TxnId} status is {Status}, not completed.", request.TransactionId, transaction.Status);
            return Results.BadRequest("Transaction is not completed.");
        }

        var customDataUserId = ExtractCustomDataUserId(transaction);
        if (customDataUserId != identityId)
        {
            logger.LogWarning("Transaction {TxnId} user_id {TxnUser} does not match authenticated user {AuthUser}.",
                request.TransactionId, customDataUserId, identityId);
            return Results.Forbid();
        }

        var totalsString = transaction.Details?.Totals?.GrandTotal
            ?? transaction.Details?.Totals?.Total
            ?? "0";
        decimal.TryParse(totalsString, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var totalCents);

        var itemsJson = await BuildItemsSnapshotAsync(dbContext, transaction, cancellationToken);

        var now = DateTime.UtcNow;
        var payment = new PaddlePayment
        {
            PaddleTransactionId = request.TransactionId,
            UserId = identityId,
            Status = "paid",
            TotalAmount = totalCents / 100m,
            Currency = transaction.CurrencyCode,
            ItemsJson = itemsJson,
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.PaddlePayments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Payment recorded via confirm: transaction {TxnId}, user {UserId}.",
            request.TransactionId, identityId);

        return Results.Ok(BuildStatusResponse(payment));
    }

    /// <summary>
    /// Snapshot the transaction's line items into the shape we surface to the dashboard:
    /// <c>[{ paddleProductId, paddlePriceId, displayName, quantity, unitPrice, currencyCode }]</c>.
    /// Display names are looked up in <c>paddle_products</c> by id (the local mirror should
    /// already have these from a prior product sync) and fall back to the price id if missing.
    /// </summary>
    internal static async Task<string?> BuildItemsSnapshotAsync(
        ApplicationDbContext dbContext,
        PaddleTransactionDto transaction,
        CancellationToken cancellationToken)
    {
        var lineItems = transaction.Details?.LineItems;
        if (lineItems is not { Count: > 0 })
            return null;

        var priceIds = lineItems.Select(l => l.PriceId).Where(p => !string.IsNullOrEmpty(p)).Distinct().ToList();
        if (priceIds.Count == 0)
            return null;

        // Pull product names (and their paddleProductId) for any prices we're snapshotting.
        var priceLookup = await dbContext.PaddlePrices
            .AsNoTracking()
            .Where(p => priceIds.Contains(p.PaddlePriceId))
            .Join(dbContext.PaddleProducts.AsNoTracking(),
                pr => pr.PaddleProductInternalId,
                pp => pp.Id,
                (pr, pp) => new { pr.PaddlePriceId, pp.PaddleProductId, pp.Name })
            .ToDictionaryAsync(x => x.PaddlePriceId, cancellationToken);

        // Paddle reports per-unit pricing on transaction line items as `unit_totals.subtotal`
        // (smallest currency unit, pre-tax/discount). If unit_totals is missing for any reason
        // we derive the unit price from the line totals (totals.subtotal / quantity) so the
        // snapshot is never silently zero.
        static string ResolveUnitPrice(PaddleTransactionLineItem l)
        {
            var unitSubtotal = l.UnitTotals?.Subtotal;
            if (!string.IsNullOrEmpty(unitSubtotal) && unitSubtotal != "0")
                return unitSubtotal;

            if (l.Quantity > 0
                && long.TryParse(l.Totals?.Subtotal, out var totalSubtotal)
                && totalSubtotal > 0)
            {
                return (totalSubtotal / l.Quantity).ToString();
            }

            return unitSubtotal ?? "0";
        }

        var snapshot = lineItems
            .Where(l => !string.IsNullOrEmpty(l.PriceId))
            .Select(l =>
            {
                priceLookup.TryGetValue(l.PriceId, out var product);
                return new
                {
                    paddleProductId = product?.PaddleProductId,
                    paddlePriceId = l.PriceId,
                    displayName = product?.Name ?? l.PriceId,
                    quantity = l.Quantity,
                    unitPrice = ResolveUnitPrice(l),
                    currencyCode = l.UnitTotals?.CurrencyCode
                        ?? l.Totals?.CurrencyCode
                        ?? transaction.CurrencyCode,
                };
            })
            .ToArray();

        return JsonSerializer.Serialize(snapshot, ItemsJsonOptions);
    }

    private static CheckoutStatusResponse BuildStatusResponse(PaddlePayment? payment)
    {
        if (payment is null)
            return new CheckoutStatusResponse(false, null, null, null, null);

        var items = ParseItems(payment.ItemsJson);
        return new CheckoutStatusResponse(
            HasUnusedPayment: true,
            PaymentId: payment.Id,
            Items: items,
            TotalAmount: payment.TotalAmount,
            Currency: payment.Currency);
    }

    private static IReadOnlyList<CheckoutItemResponse>? ParseItems(string? itemsJson)
    {
        if (string.IsNullOrWhiteSpace(itemsJson))
            return null;
        try
        {
            return JsonSerializer.Deserialize<List<CheckoutItemResponse>>(itemsJson, ItemsJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Returns <c>true</c> when the snapshot is missing OR has been written but every line item
    /// has a zero/missing unit price — the latter happens with rows produced by the old code that
    /// read from <c>line_items.unit_price</c> (which Paddle doesn't actually send) instead of
    /// <c>line_items.unit_totals.subtotal</c>. Treating zero-price snapshots as stale lets the
    /// lazy backfill fix them on the next status read instead of requiring manual SQL.
    /// </summary>
    private static bool IsItemsSnapshotStale(string? itemsJson)
    {
        if (string.IsNullOrWhiteSpace(itemsJson))
            return true;

        var items = ParseItems(itemsJson);
        if (items is null || items.Count == 0)
            return true;

        return items.All(i => string.IsNullOrEmpty(i.UnitPrice) || i.UnitPrice == "0");
    }

    private static string? ExtractCustomDataUserId(PaddleTransactionDto transaction)
    {
        if (transaction.CustomData is not { ValueKind: JsonValueKind.Object } cd)
            return null;
        return cd.TryGetProperty("user_id", out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;
    }
}

public sealed record CheckoutStatusResponse(
    bool HasUnusedPayment,
    int? PaymentId,
    IReadOnlyList<CheckoutItemResponse>? Items,
    decimal? TotalAmount,
    string? Currency);

public sealed record CheckoutItemResponse(
    string? PaddleProductId,
    string PaddlePriceId,
    string DisplayName,
    int Quantity,
    /// <summary>Smallest currency unit (Paddle convention) — divide by 100 for major-unit display in most currencies.</summary>
    string UnitPrice,
    string CurrencyCode);

public sealed record ConfirmTransactionRequest(string TransactionId);
