using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Services.LemonSqueezy;

namespace Odin.Api.Endpoints.Webhooks;

public static class LemonSqueezyWebhookEndpoints
{
    public const string Path = "/webhooks/lemonsqueezy";

    public static void MapLemonSqueezyWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost(Path, HandleWebhook)
            .AllowAnonymous()
            .DisableAntiforgery()
            .WithTags("Webhooks")
            .WithSummary("Lemon Squeezy webhook (HMAC-verified POST)");
    }

    private static async Task<IResult> HandleWebhook(
        HttpContext httpContext,
        IOptions<LemonSqueezyOptions> options,
        ApplicationDbContext dbContext,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger("Odin.Api.Webhooks.LemonSqueezy");
        var secret = options.Value.WebhookSigningSecret;

        if (string.IsNullOrWhiteSpace(secret))
        {
            logger.LogWarning("Lemon Squeezy webhook ignored: WebhookSigningSecret is not configured.");
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }

        using var ms = new MemoryStream();
        await httpContext.Request.Body.CopyToAsync(ms, cancellationToken);
        var rawBody = ms.ToArray();

        var signature = httpContext.Request.Headers["X-Signature"].FirstOrDefault();
        if (!LemonSqueezyWebhookSignatureVerifier.IsValid(rawBody, secret, signature))
        {
            logger.LogWarning("Lemon Squeezy webhook rejected: invalid or missing X-Signature.");
            return Results.Unauthorized();
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(rawBody);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Lemon Squeezy webhook: JSON parse failed after signature verification.");
            return Results.BadRequest();
        }

        using (doc)
        {
            var eventName = httpContext.Request.Headers["X-Event-Name"].FirstOrDefault();
            if (string.IsNullOrEmpty(eventName)
                && doc.RootElement.TryGetProperty("meta", out var metaEl)
                && metaEl.TryGetProperty("event_name", out var en)
                && en.ValueKind == JsonValueKind.String)
                eventName = en.GetString();

            logger.LogInformation(
                "Lemon Squeezy webhook received: event={EventName}, bytes={Length}",
                eventName ?? "(unknown)", rawBody.Length);

            switch (eventName)
            {
                case "order_created":
                    await HandleOrderCreated(doc, dbContext, logger, cancellationToken);
                    break;
                case "order_refunded":
                    await HandleOrderRefunded(doc, dbContext, logger, cancellationToken);
                    break;
                default:
                    logger.LogInformation("Lemon Squeezy webhook: unhandled event {EventName}, acknowledging.", eventName);
                    break;
            }
        }

        return Results.Ok();
    }

    private static async Task HandleOrderCreated(
        JsonDocument doc,
        ApplicationDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var data = doc.RootElement.GetProperty("data");
        var attrs = data.GetProperty("attributes");
        var lsOrderId = data.GetProperty("id").GetString()!;

        var existing = await dbContext.LemonSqueezyPayments
            .FirstOrDefaultAsync(p => p.LemonSqueezyOrderId == lsOrderId, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation("Lemon Squeezy order {LsOrderId} already recorded, skipping.", lsOrderId);
            return;
        }

        string? userId = null;
        if (doc.RootElement.TryGetProperty("meta", out var meta)
            && meta.TryGetProperty("custom_data", out var customData)
            && customData.TryGetProperty("user_id", out var uid))
        {
            userId = uid.GetString();
        }

        if (string.IsNullOrEmpty(userId))
        {
            logger.LogWarning("Lemon Squeezy order_created missing custom_data.user_id for LS order {LsOrderId}.", lsOrderId);
            return;
        }

        var totalCents = attrs.TryGetProperty("total", out var totalEl) ? totalEl.GetInt64() : 0;
        var currency = attrs.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "USD" : "USD";
        var status = attrs.TryGetProperty("status", out var st) ? st.GetString() ?? "paid" : "paid";

        string? receiptUrl = null;
        if (attrs.TryGetProperty("urls", out var urls) && urls.TryGetProperty("receipt", out var receipt))
            receiptUrl = receipt.GetString();

        var now = DateTime.UtcNow;
        dbContext.LemonSqueezyPayments.Add(new LemonSqueezyPayment
        {
            LemonSqueezyOrderId = lsOrderId,
            UserId = userId,
            Status = status,
            TotalAmount = totalCents / 100m,
            Currency = currency,
            ReceiptUrl = receiptUrl,
            CreatedAt = now,
            UpdatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Lemon Squeezy payment recorded: LS order {LsOrderId}, user {UserId}, total {Total} {Currency}.",
            lsOrderId, userId, totalCents / 100m, currency);
    }

    private static async Task HandleOrderRefunded(
        JsonDocument doc,
        ApplicationDbContext dbContext,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var lsOrderId = doc.RootElement.GetProperty("data").GetProperty("id").GetString()!;

        var payment = await dbContext.LemonSqueezyPayments
            .FirstOrDefaultAsync(p => p.LemonSqueezyOrderId == lsOrderId, cancellationToken);

        if (payment is null)
        {
            logger.LogWarning("Lemon Squeezy order_refunded for unknown LS order {LsOrderId}.", lsOrderId);
            return;
        }

        payment.Status = "refunded";
        payment.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Lemon Squeezy payment {Id} marked refunded (LS order {LsOrderId}).", payment.Id, lsOrderId);
    }
}
