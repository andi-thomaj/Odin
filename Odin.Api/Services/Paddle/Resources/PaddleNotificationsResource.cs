using Odin.Api.Services.Paddle.Models.Common;
using Odin.Api.Services.Paddle.Models.Notifications;

namespace Odin.Api.Services.Paddle.Resources;

public interface IPaddleNotificationsResource
{
    Task<PaddleListEnvelope<PaddleNotificationDto>> ListAsync(PaddleNotificationListQuery? query = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<PaddleNotificationDto> ListAllAsync(PaddleNotificationListQuery? query = null, CancellationToken cancellationToken = default);
    Task<PaddleNotificationDto> GetAsync(string notificationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replays a previously delivered notification through the same destination. Only notifications
    /// with <c>origin == "event"</c> are eligible — replays of replays are rejected by Paddle.
    /// Notifications older than 90 days are no longer retained.
    /// </summary>
    Task<PaddleReplayResponse> ReplayAsync(string notificationId, CancellationToken cancellationToken = default);
}

public sealed class PaddleNotificationsResource(IPaddleApiClient client) : IPaddleNotificationsResource
{
    private const string BasePath = "notifications";

    public Task<PaddleListEnvelope<PaddleNotificationDto>> ListAsync(
        PaddleNotificationListQuery? query = null,
        CancellationToken cancellationToken = default)
        => client.GetPageAsync<PaddleNotificationDto>(BasePath, query?.ToQuery(), cancellationToken);

    public IAsyncEnumerable<PaddleNotificationDto> ListAllAsync(
        PaddleNotificationListQuery? query = null,
        CancellationToken cancellationToken = default)
        => client.GetAllAsync<PaddleNotificationDto>(BasePath, query?.ToQuery(), cancellationToken);

    public Task<PaddleNotificationDto> GetAsync(string notificationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationId);
        return client.GetAsync<PaddleNotificationDto>($"{BasePath}/{notificationId}", query: null, cancellationToken);
    }

    public Task<PaddleReplayResponse> ReplayAsync(string notificationId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(notificationId);
        // The replay endpoint accepts no body; using the notification id as the idempotency key
        // means accidental double-calls within the dedup window collapse into a single replay.
        return client.PostAsync<PaddleReplayResponse>($"{BasePath}/{notificationId}/replay",
            idempotencyKey: $"replay:{notificationId}",
            cancellationToken: cancellationToken);
    }
}

public sealed class PaddleNotificationListQuery
{
    public string? Status { get; set; }
    public string? NotificationSettingId { get; set; }
    public string? Filter { get; set; }
    public string? Search { get; set; }
    public DateTimeOffset? From { get; set; }
    public DateTimeOffset? To { get; set; }
    public string? After { get; set; }
    public int? PerPage { get; set; }
    public string? OrderBy { get; set; }

    public Dictionary<string, string?> ToQuery() => new()
    {
        ["status"] = Status,
        ["notification_setting_id"] = NotificationSettingId,
        ["filter"] = Filter,
        ["search"] = Search,
        ["from"] = From?.ToString("O"),
        ["to"] = To?.ToString("O"),
        ["after"] = After,
        ["per_page"] = PerPage?.ToString(),
        ["order_by"] = OrderBy,
    };
}
