using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.CheckoutManagement;
using Odin.Api.Services.Paddle;
using Odin.Api.Services.Paddle.Models.Transactions;
using Odin.Api.Services.Paddle.Sync;

namespace Odin.Api.Endpoints.Webhooks;

public static class PaddleWebhookEndpoints
{
    public const string Path = "/webhooks/paddle";

    public static void MapPaddleWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(Path, HandleWebhook)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithTags("Webhooks")
            .WithSummary("Paddle webhook (HMAC-verified POST)");
    }

    private static async Task<IResult> HandleWebhook(
        HttpContext httpContext,
        IOptions<PaddleOptions> options,
        ApplicationDbContext dbContext,
        IPaddleNotificationStore notificationStore,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Odin.Api.Webhooks.Paddle");
        var secret = options.Value.WebhookSecret;

        if (string.IsNullOrWhiteSpace(secret))
        {
            logger.LogWarning("Paddle webhook ignored: WebhookSecret is not configured.");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        using var ms = new MemoryStream();
        await httpContext.Request.Body.CopyToAsync(ms, cancellationToken);
        var rawBody = ms.ToArray();

        var signature = httpContext.Request.Headers["Paddle-Signature"].FirstOrDefault();
        if (!PaddleWebhookSignatureVerifier.IsValid(rawBody, secret, signature))
        {
            logger.LogWarning("Paddle webhook rejected: invalid or missing Paddle-Signature.");
            return Results.Unauthorized();
        }

        var rawJson = Encoding.UTF8.GetString(rawBody);
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawBody);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Paddle webhook: JSON parse failed after signature verification.");
            return Results.BadRequest();
        }

        // Persist the raw payload first so we have a durable audit-and-replay trail even if
        // projection logic below throws or is later changed.
        PaddleNotification? logRow = null;
        try
        {
            logRow = await notificationStore.RecordWebhookAsync(rawJson, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Paddle webhook: failed to persist notification log entry.");
        }

        using (doc)
        {
            var eventType = doc.RootElement.TryGetProperty("event_type", out var et)
                ? et.GetString()
                : null;

            logger.LogInformation(
                "Paddle webhook received: event={EventType}, bytes={Length}, logId={LogId}",
                eventType ?? "(unknown)", rawBody.Length, logRow?.Id);

            try
            {
                switch (eventType)
                {
                    case "transaction.completed":
                        await HandleTransactionCompleted(doc, dbContext, logger, cancellationToken);
                        break;
                    case "transaction.refunded":
                        await HandleTransactionRefunded(doc, dbContext, logger, cancellationToken);
                        break;
                    default:
                        logger.LogInformation("Paddle webhook: unhandled event {EventType}, acknowledging.", eventType);
                        break;
                }

                if (logRow is not null)
                    await notificationStore.MarkProcessedAsync(logRow.Id, cancellationToken);
            }
            catch (Exception ex) when (logRow is not null)
            {
                logger.LogError(ex, "Paddle webhook projection failed for {EventType}, log id {LogId}.", eventType, logRow.Id);
                await notificationStore.MarkFailedAsync(logRow.Id, ex.Message, cancellationToken);
                throw;
            }
        }

        return Results.Ok();
    }

    private static async Task HandleTransactionCompleted(
        JsonDocument doc,
        ApplicationDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var data = doc.RootElement.GetProperty("data");
        var transactionId = data.GetProperty("id").GetString()!;

        var existing = await dbContext.PaddlePayments
            .FirstOrDefaultAsync(p => p.PaddleTransactionId == transactionId, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation("Paddle transaction {TransactionId} already recorded, skipping.", transactionId);
            return;
        }

        string? userId = null;
        if (data.TryGetProperty("custom_data", out var customData)
            && customData.TryGetProperty("user_id", out var uid))
        {
            userId = uid.GetString();
        }

        if (string.IsNullOrEmpty(userId))
        {
            logger.LogWarning("Paddle transaction.completed missing custom_data.user_id for transaction {TransactionId}.", transactionId);
            return;
        }

        // Deserialize the webhook's transaction data into the same DTO the typed Paddle client uses.
        // Lets us reuse BuildItemsSnapshotAsync from CheckoutEndpoints — single source of truth
        // for the items snapshot regardless of whether the payment was confirmed via webhook or API.
        PaddleTransactionDto? transactionDto = null;
        try
        {
            transactionDto = data.Deserialize<PaddleTransactionDto>(PaddleJson.Options);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Paddle webhook: failed to parse transaction payload for {TransactionId}.", transactionId);
        }

        var totalCents = 0m;
        var currency = "USD";
        string? receiptUrl = null;
        string? itemsJson = null;

        if (transactionDto is not null)
        {
            var totalsString = transactionDto.Details?.Totals?.GrandTotal
                ?? transactionDto.Details?.Totals?.Total
                ?? "0";
            decimal.TryParse(totalsString, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out totalCents);

            currency = transactionDto.CurrencyCode;
            receiptUrl = transactionDto.Checkout?.Url;
            itemsJson = await CheckoutEndpoints.BuildItemsSnapshotAsync(dbContext, transactionDto, cancellationToken);
        }

        var now = DateTime.UtcNow;
        dbContext.PaddlePayments.Add(new PaddlePayment
        {
            PaddleTransactionId = transactionId,
            UserId = userId,
            Status = "paid",
            TotalAmount = totalCents / 100m,
            Currency = currency,
            ReceiptUrl = receiptUrl,
            ItemsJson = itemsJson,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Paddle payment recorded: transaction {TransactionId}, user {UserId}, total {Total} {Currency}.",
            transactionId, userId, totalCents / 100m, currency);
    }

    private static async Task HandleTransactionRefunded(
        JsonDocument doc,
        ApplicationDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var transactionId = doc.RootElement.GetProperty("data").GetProperty("id").GetString()!;

        var payment = await dbContext.PaddlePayments
            .FirstOrDefaultAsync(p => p.PaddleTransactionId == transactionId, cancellationToken);

        if (payment is null)
        {
            logger.LogWarning("Paddle transaction.refunded for unknown transaction {TransactionId}.", transactionId);
            return;
        }

        payment.Status = "refunded";
        payment.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Paddle payment {Id} marked refunded (transaction {TransactionId}).", payment.Id, transactionId);
    }
}
