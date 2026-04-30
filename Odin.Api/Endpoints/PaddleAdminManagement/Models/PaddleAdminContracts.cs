namespace Odin.Api.Endpoints.PaddleAdminManagement.Models;

public sealed record PaddleSyncResultResponse(
    string Resource,
    int Inserted,
    int Updated,
    int Skipped,
    int Failed,
    int Total,
    IReadOnlyList<string> Errors);

public sealed record PaddleNotificationListItem(
    int Id,
    string PaddleNotificationId,
    string EventType,
    string? PaddleEventId,
    string? Origin,
    DateTimeOffset? OccurredAt,
    DateTime ReceivedAt,
    DateTime? ProcessedAt,
    string ProcessedStatus,
    int ProcessingAttempts,
    string? ProcessingError,
    string Source);

public sealed record PaddleNotificationDetail(
    int Id,
    string PaddleNotificationId,
    string EventType,
    string? PaddleEventId,
    string? Origin,
    DateTimeOffset? OccurredAt,
    DateTime ReceivedAt,
    DateTime? ProcessedAt,
    string ProcessedStatus,
    int ProcessingAttempts,
    string? ProcessingError,
    string Source,
    string Payload);

public sealed record ReplayNotificationResponse(string NewPaddleNotificationId);

public sealed record BackfillNotificationsRequest(
    DateTimeOffset? From,
    DateTimeOffset? To,
    string? Status);

public sealed record TransactionSyncRequest(DateTimeOffset? BilledAtFrom);
