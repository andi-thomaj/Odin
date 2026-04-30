using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

/// <summary>
/// Local mirror of a Paddle transaction — separate from the existing <see cref="PaddlePayment"/>
/// entity. <see cref="PaddlePayment"/> is the application-level "this user has an unused paid credit"
/// flag wired into checkout/order fulfilment; this entity is the Paddle-shape mirror used by sync
/// (so we can run reports, reconcile, and project new entitlements without re-hitting Paddle).
/// </summary>
public class PaddleTransaction
{
    public int Id { get; set; }

    /// <summary>Paddle transaction id, prefixed <c>txn_</c>.</summary>
    public required string PaddleTransactionId { get; set; }

    public required string Status { get; set; }
    public string? PaddleCustomerId { get; set; }
    public string? PaddleSubscriptionId { get; set; }
    public string? InvoiceId { get; set; }
    public string? InvoiceNumber { get; set; }
    public string? Origin { get; set; }
    public string? CollectionMode { get; set; }
    public required string CurrencyCode { get; set; }

    /// <summary>Smallest currency unit (Paddle convention), as a string.</summary>
    public string? Subtotal { get; set; }
    public string? TaxTotal { get; set; }
    public string? DiscountTotal { get; set; }
    public string? GrandTotal { get; set; }

    public DateTimeOffset? BilledAt { get; set; }
    public DateTimeOffset? PaddleCreatedAt { get; set; }
    public DateTimeOffset? PaddleUpdatedAt { get; set; }
    public DateTime LastSyncedAt { get; set; }

    /// <summary>Full Paddle transaction document, stored as <c>jsonb</c>.</summary>
    public required string RawJson { get; set; }

    public string? CustomData { get; set; }
}

public class PaddleTransactionConfiguration : IEntityTypeConfiguration<PaddleTransaction>
{
    public void Configure(EntityTypeBuilder<PaddleTransaction> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PaddleTransactionId).IsRequired().HasMaxLength(64);
        builder.HasIndex(e => e.PaddleTransactionId).IsUnique();

        builder.Property(e => e.Status).IsRequired().HasMaxLength(32);
        builder.HasIndex(e => e.Status);

        builder.Property(e => e.PaddleCustomerId).HasMaxLength(64);
        builder.HasIndex(e => e.PaddleCustomerId);

        builder.Property(e => e.PaddleSubscriptionId).HasMaxLength(64);
        builder.HasIndex(e => e.PaddleSubscriptionId);

        builder.Property(e => e.InvoiceId).HasMaxLength(64);
        builder.Property(e => e.InvoiceNumber).HasMaxLength(64);
        builder.Property(e => e.Origin).HasMaxLength(32);
        builder.Property(e => e.CollectionMode).HasMaxLength(16);
        builder.Property(e => e.CurrencyCode).IsRequired().HasMaxLength(8);

        builder.Property(e => e.Subtotal).HasMaxLength(32);
        builder.Property(e => e.TaxTotal).HasMaxLength(32);
        builder.Property(e => e.DiscountTotal).HasMaxLength(32);
        builder.Property(e => e.GrandTotal).HasMaxLength(32);

        builder.Property(e => e.LastSyncedAt).IsRequired();
        builder.Property(e => e.RawJson).IsRequired().HasColumnType("jsonb");
        builder.Property(e => e.CustomData).HasColumnType("jsonb");

        builder.ToTable("paddle_transactions");
    }
}
