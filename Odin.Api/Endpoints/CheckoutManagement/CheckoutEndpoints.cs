using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;
using Odin.Api.Data;
using Odin.Api.Data.Entities;

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
        IOptions<PaddleOptions> paddleOptions,
        IHttpClientFactory httpClientFactory,
        ILoggerFactory loggerFactory,
        ConfirmTransactionRequest request,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Odin.Api.Checkout.Confirm");
        var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub")
                         ?? string.Empty;
        var cfg = paddleOptions.Value;

        if (string.IsNullOrWhiteSpace(cfg.ApiKey))
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

        if (string.IsNullOrWhiteSpace(request.TransactionId))
            return Results.BadRequest("transactionId is required.");

        // Already recorded (idempotent)?
        var existing = await dbContext.PaddlePayments
            .FirstOrDefaultAsync(p => p.PaddleTransactionId == request.TransactionId, cancellationToken);
        if (existing is not null)
        {
            var alreadyStatus = existing.UserId == identityId && existing.Status == "paid" && existing.OrderId == null;
            return Results.Ok(new CheckoutStatusResponse(alreadyStatus, alreadyStatus ? existing.Id : null));
        }

        // Fetch transaction from Paddle API
        var client = httpClientFactory.CreateClient("Paddle");
        using var req = new HttpRequestMessage(HttpMethod.Get,
            $"{cfg.ApiBaseUrl.TrimEnd('/')}/transactions/{request.TransactionId}");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg.ApiKey);

        using var response = await client.SendAsync(req, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Paddle API returned {Status} for transaction {TxnId}.",
                (int)response.StatusCode, request.TransactionId);
            return Results.BadRequest("Could not verify this transaction with Paddle.");
        }

        using var doc = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var data = doc.RootElement.GetProperty("data");
        var status = data.TryGetProperty("status", out var st) ? st.GetString() : null;
        if (status is not "completed" and not "paid")
        {
            logger.LogInformation("Transaction {TxnId} status is {Status}, not completed.", request.TransactionId, status);
            return Results.BadRequest("Transaction is not completed.");
        }

        // Verify custom_data.user_id matches the authenticated user
        string? txnUserId = null;
        if (data.TryGetProperty("custom_data", out var cd) && cd.TryGetProperty("user_id", out var uid))
            txnUserId = uid.GetString();

        if (txnUserId != identityId)
        {
            logger.LogWarning("Transaction {TxnId} user_id {TxnUser} does not match authenticated user {AuthUser}.",
                request.TransactionId, txnUserId, identityId);
            return Results.Forbid();
        }

        // Extract totals
        var details = data.TryGetProperty("details", out var det) ? det : data;
        var totalStr = details.TryGetProperty("totals", out var totals) && totals.TryGetProperty("total", out var totalEl)
            ? totalEl.GetString() ?? "0"
            : "0";
        decimal.TryParse(totalStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var totalCents);

        var currency = data.TryGetProperty("currency_code", out var cur) ? cur.GetString() ?? "USD" : "USD";

        var now = DateTime.UtcNow;
        var payment = new PaddlePayment
        {
            PaddleTransactionId = request.TransactionId,
            UserId = identityId,
            Status = "paid",
            TotalAmount = totalCents / 100m,
            Currency = currency,
            CreatedAt = now,
            UpdatedAt = now
        };
        dbContext.PaddlePayments.Add(payment);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Payment recorded via confirm: transaction {TxnId}, user {UserId}.",
            request.TransactionId, identityId);

        return Results.Ok(new CheckoutStatusResponse(true, payment.Id));
    }
}

public sealed record CheckoutStatusResponse(bool HasUnusedPayment, int? PaymentId);
public sealed record ConfirmTransactionRequest(string TransactionId);
