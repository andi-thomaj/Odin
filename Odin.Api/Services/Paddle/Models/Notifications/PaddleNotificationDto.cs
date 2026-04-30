using System.Text.Json;
using Odin.Api.Services.Paddle.Models.Common;

namespace Odin.Api.Services.Paddle.Models.Notifications;

/// <summary>
/// Snapshot of a Paddle notification (a single delivery attempt of a domain event to a webhook destination).
/// IDs are prefixed <c>ntf_</c>. Notifications older than 90 days are not retained by Paddle.
/// </summary>
public sealed class PaddleNotificationDto
{
    public string Id { get; set; } = "";

    /// <summary>Event type, e.g. <c>transaction.completed</c>, <c>subscription.activated</c>.</summary>
    public string Type { get; set; } = "";

    public string Status { get; set; } = "";

    /// <summary>The full event payload as Paddle delivered it. Treat as opaque JSON; deserialize on demand.</summary>
    public JsonElement? Payload { get; set; }

    public DateTimeOffset? OccurredAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? ReplayedAt { get; set; }

    /// <summary><c>event</c>, <c>replay</c>, or other origin tag. Only <c>event</c>-origin notifications are eligible for replay.</summary>
    public string? Origin { get; set; }

    public DateTimeOffset? LastAttemptAt { get; set; }
    public DateTimeOffset? RetryAt { get; set; }
    public int TimesAttempted { get; set; }

    /// <summary>The notification setting (destination) that was the target of this delivery, prefixed <c>ntfset_</c>.</summary>
    public string? NotificationSettingId { get; set; }
}

public sealed class PaddleReplayResponse
{
    /// <summary>Returns the new notification id (the replay), prefixed <c>ntf_</c>.</summary>
    public string NotificationId { get; set; } = "";
}

public sealed class PaddleEventDto
{
    public string EventId { get; set; } = "";
    public string EventType { get; set; } = "";
    public DateTimeOffset OccurredAt { get; set; }
    public string? NotificationId { get; set; }
    public JsonElement? Data { get; set; }
}
