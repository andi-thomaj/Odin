using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

/// <summary>
/// Local mirror of a Paddle price. Paddle prices store amounts as integer-string
/// in the smallest currency unit (e.g. <c>"4999"</c> = $49.99), so we keep it as
/// <see cref="UnitPriceAmount"/> string + <see cref="UnitPriceCurrency"/> rather
/// than coercing to <c>decimal</c> here — that conversion belongs at the surface.
/// </summary>
public class PaddlePrice
{
    public int Id { get; set; }

    /// <summary>Paddle price id, prefixed <c>pri_</c>.</summary>
    public required string PaddlePriceId { get; set; }

    /// <summary>FK to the local <see cref="PaddleProduct"/> row.</summary>
    public int PaddleProductInternalId { get; set; }
    public PaddleProduct? Product { get; set; }

    /// <summary>Paddle product id (denormalized for lookups without a join).</summary>
    public required string PaddleProductId { get; set; }

    public string? Description { get; set; }
    public string? Name { get; set; }
    public string? Type { get; set; }

    /// <summary>Smallest currency unit, as Paddle returns it.</summary>
    public required string UnitPriceAmount { get; set; }
    public required string UnitPriceCurrency { get; set; }

    /// <summary>Recurring billing interval, when set: <c>day</c>, <c>week</c>, <c>month</c>, <c>year</c>.</summary>
    public string? BillingCycleInterval { get; set; }
    public int? BillingCycleFrequency { get; set; }

    public string? TrialPeriodInterval { get; set; }
    public int? TrialPeriodFrequency { get; set; }

    public string? TaxMode { get; set; }
    public required string Status { get; set; }

    public string? CustomData { get; set; }

    public DateTimeOffset? PaddleCreatedAt { get; set; }
    public DateTimeOffset? PaddleUpdatedAt { get; set; }
    public DateTime LastSyncedAt { get; set; }
}

public class PaddlePriceConfiguration : IEntityTypeConfiguration<PaddlePrice>
{
    public void Configure(EntityTypeBuilder<PaddlePrice> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PaddlePriceId).IsRequired().HasMaxLength(64);
        builder.HasIndex(e => e.PaddlePriceId).IsUnique();

        builder.Property(e => e.PaddleProductId).IsRequired().HasMaxLength(64);
        builder.HasIndex(e => e.PaddleProductId);

        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Name).HasMaxLength(200);
        builder.Property(e => e.Type).HasMaxLength(32);

        builder.Property(e => e.UnitPriceAmount).IsRequired().HasMaxLength(32);
        builder.Property(e => e.UnitPriceCurrency).IsRequired().HasMaxLength(8);

        builder.Property(e => e.BillingCycleInterval).HasMaxLength(16);
        builder.Property(e => e.TrialPeriodInterval).HasMaxLength(16);

        builder.Property(e => e.TaxMode).HasMaxLength(32);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(32);

        builder.Property(e => e.CustomData).HasColumnType("jsonb");
        builder.Property(e => e.LastSyncedAt).IsRequired();

        builder.ToTable("paddle_prices");
    }
}
