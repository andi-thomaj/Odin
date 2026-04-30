using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Services.Paddle.Models.Notifications;
using Odin.Api.Services.Paddle.Resources;

namespace Odin.Api.Services.Paddle.Sync;

public interface IPaddleNotificationStore
{
    /// <summary>
    /// Persist a freshly-received webhook payload into <c>paddle_notifications</c>. Idempotent:
    /// repeated calls with the same notification id are no-ops.
    /// </summary>
    Task<PaddleNotification> RecordWebhookAsync(string rawJson, CancellationToken cancellationToken = default);

    /// <summary>Marks a notification as projected so future sweeps skip it.</summary>
    Task MarkProcessedAsync(int notificationId, CancellationToken cancellationToken = default);

    /// <summary>Marks a notification as failed and records the error.</summary>
    Task MarkFailedAsync(int notificationId, string error, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asks Paddle to redeliver a notification by id (POST /notifications/{id}/replay).
    /// Returns the replay's new notification id. Used when our projection logic changes and
    /// we need Paddle to re-send a failed event through our webhook handler.
    /// </summary>
    Task<string> ReplayWithPaddleAsync(string paddleNotificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pulls notifications directly from Paddle's notifications API (rather than waiting for
    /// webhooks) and records them locally. Useful when we suspect we missed deliveries — for
    /// example after a deploy outage.
    /// </summary>
    Task<PaddleSyncResult> BackfillFromPaddleAsync(PaddleNotificationListQuery? query = null, CancellationToken cancellationToken = default);
}

public sealed class PaddleNotificationStore(
    ApplicationDbContext dbContext,
    IPaddleNotificationsResource notificationsResource,
    ILogger<PaddleNotificationStore> logger) : IPaddleNotificationStore
{
    public async Task<PaddleNotification> RecordWebhookAsync(string rawJson, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawJson);

        var (notificationId, eventId, eventType, occurredAt) = ParseHeader(rawJson);
        if (string.IsNullOrEmpty(notificationId))
            throw new InvalidOperationException("Webhook payload missing notification_id.");

        var existing = await dbContext.PaddleNotifications
            .FirstOrDefaultAsync(n => n.PaddleNotificationId == notificationId, cancellationToken);
        if (existing is not null)
            return existing;

        var entity = new PaddleNotification
        {
            PaddleNotificationId = notificationId,
            PaddleEventId = eventId,
            EventType = eventType ?? "unknown",
            Origin = "event",
            Payload = rawJson,
            OccurredAt = occurredAt,
            ReceivedAt = DateTime.UtcNow,
            ProcessedStatus = "pending",
            Source = "webhook",
        };
        dbContext.PaddleNotifications.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return entity;
    }

    public async Task MarkProcessedAsync(int notificationId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PaddleNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);
        if (entity is null) return;

        entity.ProcessedStatus = "processed";
        entity.ProcessedAt = DateTime.UtcNow;
        entity.ProcessingAttempts++;
        entity.ProcessingError = null;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFailedAsync(int notificationId, string error, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.PaddleNotifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, cancellationToken);
        if (entity is null) return;

        entity.ProcessedStatus = "failed";
        entity.ProcessingAttempts++;
        entity.ProcessingError = error.Length > 4000 ? error[..4000] : error;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> ReplayWithPaddleAsync(string paddleNotificationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(paddleNotificationId);
        var response = await notificationsResource.ReplayAsync(paddleNotificationId, cancellationToken);
        logger.LogInformation("Paddle replay requested for {Source} -> new notification {Replay}.",
            paddleNotificationId, response.NotificationId);
        return response.NotificationId;
    }

    public async Task<PaddleSyncResult> BackfillFromPaddleAsync(
        PaddleNotificationListQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        var result = new PaddleSyncResult { Resource = "notifications" };
        var existingIds = await dbContext.PaddleNotifications
            .Select(n => n.PaddleNotificationId)
            .ToListAsync(cancellationToken);
        var seen = existingIds.ToHashSet(StringComparer.Ordinal);

        await foreach (var dto in notificationsResource.ListAllAsync(query ?? new PaddleNotificationListQuery { PerPage = 200 }, cancellationToken))
        {
            try
            {
                if (seen.Contains(dto.Id))
                {
                    result.Skipped++;
                    continue;
                }

                var payload = dto.Payload?.GetRawText() ?? "{}";
                dbContext.PaddleNotifications.Add(new PaddleNotification
                {
                    PaddleNotificationId = dto.Id,
                    EventType = dto.Type,
                    Origin = dto.Origin,
                    Payload = payload,
                    OccurredAt = dto.OccurredAt,
                    ReceivedAt = DateTime.UtcNow,
                    ProcessedStatus = "pending",
                    Source = "backfill",
                });
                seen.Add(dto.Id);
                result.Inserted++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Notification backfill: failed for {Id}.", dto.Id);
                result.Failed++;
                result.Errors.Add($"{dto.Id}: {ex.Message}");
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return result;
    }

    private static (string? notificationId, string? eventId, string? eventType, DateTimeOffset? occurredAt) ParseHeader(string rawJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            var root = doc.RootElement;
            var notificationId = TryGetString(root, "notification_id");
            var eventId = TryGetString(root, "event_id");
            var eventType = TryGetString(root, "event_type");
            var occurredAt = TryGetDate(root, "occurred_at");
            return (notificationId, eventId, eventType, occurredAt);
        }
        catch (JsonException)
        {
            return (null, null, null, null);
        }
    }

    private static string? TryGetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()
            : null;

    private static DateTimeOffset? TryGetDate(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String)
            return null;
        return DateTimeOffset.TryParse(v.GetString(), out var parsed) ? parsed : null;
    }
}
