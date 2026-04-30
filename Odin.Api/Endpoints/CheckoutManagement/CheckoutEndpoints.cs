using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Services.Paddle;
using Odin.Api.Services.Paddle.Models.Transactions;
using Odin.Api.Services.Paddle.Resources;

namespace Odin.Api.Endpoints.CheckoutManagement;

public static class CheckoutEndpoints
{
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
        CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub")
                         ?? string.Empty;

        var payment = await dbContext.PaddlePayments
            .AsNoTracking()
            .Where(p => p.UserId == identityId && p.Status == "paid" && p.OrderId == null)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        return Results.Ok(new CheckoutStatusResponse(payment is not null, payment?.Id));
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
            var alreadyStatus = existing.UserId == identityId && existing.Status == "paid" && existing.OrderId == null;
            return Results.Ok(new CheckoutStatusResponse(alreadyStatus, alreadyStatus ? existing.Id : null));
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

        var now = DateTime.UtcNow;
        var payment = new PaddlePayment
        {
            PaddleTransactionId = request.TransactionId,
            UserId = identityId,
            Status = "paid",
            TotalAmount = totalCents / 100m,
            Currency = transaction.CurrencyCode,
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.PaddlePayments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Payment recorded via confirm: transaction {TxnId}, user {UserId}.",
            request.TransactionId, identityId);

        return Results.Ok(new CheckoutStatusResponse(true, payment.Id));
    }

    private static string? ExtractCustomDataUserId(PaddleTransactionDto transaction)
    {
        if (transaction.CustomData is not { ValueKind: System.Text.Json.JsonValueKind.Object } cd)
            return null;
        return cd.TryGetProperty("user_id", out var v) && v.ValueKind == System.Text.Json.JsonValueKind.String
            ? v.GetString()
            : null;
    }
}

public sealed record CheckoutStatusResponse(bool HasUnusedPayment, int? PaymentId);
public sealed record ConfirmTransactionRequest(string TransactionId);
