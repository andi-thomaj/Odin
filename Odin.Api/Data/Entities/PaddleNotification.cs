using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

/// <summary>
/// Local mirror of a Paddle notification (a single delivery attempt of a webhook event).
/// Acts as the source-of-truth audit log: every webhook we receive lands here verbatim
/// before any projection runs, and Paddle's <c>POST /notifications/{id}/replay</c> endpoint
/// can re-deliver a row that failed to project. The <see cref="ProcessedAt"/>/<see cref="ProcessedStatus"/>
/// fields track *our* projection state, not Paddle's delivery state.
/// </summary>
public class PaddleNotification
{
    public int Id { get; set; }

    /// <summary>Paddle notification id, prefixed <c>ntf_</c>. Unique — used for idempotent processing.</summary>
    public required string PaddleNotificationId { get; set; }

    /// <summary>Event type, e.g. <c>transaction.completed</c>.</summary>
    public required string EventType { get; set; }

    /// <summary>Original Paddle event id, prefixed <c>evt_</c>, when present.</summary>
    public string? PaddleEventId { get; set; }

    /// <summary>Origin tag from Paddle: <c>event</c>, <c>replay</c>, etc. Only <c>event</c>-origin can be replayed.</summary>
    public string? Origin { get; set; }

    /// <summary>Raw JSON payload as Paddle delivered it. Stored as <c>jsonb</c> in PostgreSQL.</summary>
    public required string Payload { get; set; }

    /// <summary>When Paddle says the originating event happened.</summary>
    public DateTimeOffset? OccurredAt { get; set; }

    /// <summary>When this row was inserted by our webhook handler or backfill job.</summary>
    public DateTime ReceivedAt { get; set; }

    /// <summary>When our projection logic finished running on this row, if it succeeded.</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>One of <c>pending</c>, <c>processed</c>, <c>failed</c>, <c>skipped</c>.</summary>
    public required string ProcessedStatus { get; set; }

    /// <summary>Last error from a projection attempt, when <see cref="ProcessedStatus"/> is <c>failed</c>.</summary>
    public string? ProcessingError { get; set; }

    public int ProcessingAttempts { get; set; }

    /// <summary>Source of this row: <c>webhook</c> (received via our /webhooks/paddle endpoint) or <c>backfill</c> (pulled from Paddle's notifications API).</summary>
    public required string Source { get; set; }
}

public class PaddleNotificationConfiguration : IEntityTypeConfiguration<PaddleNotification>
{
    public void Configure(EntityTypeBuilder<PaddleNotification> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PaddleNotificationId).IsRequired().HasMaxLength(64);
        builder.HasIndex(e => e.PaddleNotificationId).IsUnique();

        builder.Property(e => e.EventType).IsRequired().HasMaxLength(100);
        builder.HasIndex(e => e.EventType);

        builder.Property(e => e.PaddleEventId).HasMaxLength(64);
        builder.Property(e => e.Origin).HasMaxLength(32);

        builder.Property(e => e.Payload).IsRequired().HasColumnType("jsonb");

        builder.Property(e => e.OccurredAt);
        builder.Property(e => e.ReceivedAt).IsRequired();
        builder.Property(e => e.ProcessedAt);

        builder.Property(e => e.ProcessedStatus).IsRequired().HasMaxLength(16);
        builder.HasIndex(e => e.ProcessedStatus);

        builder.Property(e => e.ProcessingError).HasMaxLength(4000);
        builder.Property(e => e.ProcessingAttempts).IsRequired().HasDefaultValue(0);

        builder.Property(e => e.Source).IsRequired().HasMaxLength(16);

        builder.ToTable("paddle_notifications");
    }
}
