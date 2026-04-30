using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

/// <summary>
/// Local mirror of a Paddle subscription. The full Paddle DTO (items, scheduled change,
/// management URLs, addresses) is preserved in <see cref="RawJson"/> for cases where we
/// need fields beyond the columns called out here.
/// </summary>
public class PaddleSubscription
{
    public int Id { get; set; }

    /// <summary>Paddle subscription id, prefixed <c>sub_</c>.</summary>
    public required string PaddleSubscriptionId { get; set; }

    public required string PaddleCustomerId { get; set; }
    public required string Status { get; set; }
    public required string CurrencyCode { get; set; }

    public string? CollectionMode { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FirstBilledAt { get; set; }
    public DateTimeOffset? NextBilledAt { get; set; }
    public DateTimeOffset? PausedAt { get; set; }
    public DateTimeOffset? CanceledAt { get; set; }

    public DateTimeOffset? CurrentPeriodStartsAt { get; set; }
    public DateTimeOffset? CurrentPeriodEndsAt { get; set; }

    public string? ScheduledChangeAction { get; set; }
    public DateTimeOffset? ScheduledChangeEffectiveAt { get; set; }

    /// <summary>Full Paddle subscription document, stored as <c>jsonb</c>.</summary>
    public required string RawJson { get; set; }

    public string? CustomData { get; set; }

    public DateTimeOffset? PaddleCreatedAt { get; set; }
    public DateTimeOffset? PaddleUpdatedAt { get; set; }
    public DateTime LastSyncedAt { get; set; }
}

public class PaddleSubscriptionConfiguration : IEntityTypeConfiguration<PaddleSubscription>
{
    public void Configure(EntityTypeBuilder<PaddleSubscription> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PaddleSubscriptionId).IsRequired().HasMaxLength(64);
        builder.HasIndex(e => e.PaddleSubscriptionId).IsUnique();

        builder.Property(e => e.PaddleCustomerId).IsRequired().HasMaxLength(64);
        builder.HasIndex(e => e.PaddleCustomerId);

        builder.Property(e => e.Status).IsRequired().HasMaxLength(32);
        builder.HasIndex(e => e.Status);

        builder.Property(e => e.CurrencyCode).IsRequired().HasMaxLength(8);
        builder.Property(e => e.CollectionMode).HasMaxLength(16);
        builder.Property(e => e.ScheduledChangeAction).HasMaxLength(32);

        builder.Property(e => e.RawJson).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.CustomData).HasColumnType("jsonb");
        builder.Property(e => e.LastSyncedAt).IsRequired();

        builder.ToTable("paddle_subscriptions");
    }
}
