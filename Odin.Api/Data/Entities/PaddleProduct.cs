using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities;

/// <summary>
/// Local mirror of a Paddle product. Paddle remains source-of-truth for billing-relevant
/// fields (name, status, tax category, prices). We additionally project a few discriminator
/// columns from <c>custom_data</c> so the catalog API and order pricing can run pure SQL
/// queries without parsing JSON on every read:
///
/// <list type="bullet">
///   <item><see cref="Kind"/> — <c>service</c> (a top-level purchasable, like qpAdm) or <c>addon</c>.</item>
///   <item><see cref="ServiceType"/> — populated for <c>service</c> rows; identifies which app service this Paddle product fulfils.</item>
///   <item><see cref="ParentServiceType"/> — populated for <c>addon</c> rows; the service the addon attaches to.</item>
///   <item><see cref="AddonCode"/> — populated for <c>addon</c> rows; matches <c>ProductAddon.Code</c> for fulfillment-flag detection (EXPEDITED, Y_HAPLOGROUP, …).</item>
/// </list>
///
/// The Paddle dashboard sets these via <c>custom_data</c> on the product, e.g.
/// <c>{ "kind": "service", "service_type": "qpAdm" }</c> or
/// <c>{ "kind": "addon", "parent_service_type": "qpAdm", "addon_code": "EXPEDITED" }</c>.
/// </summary>
public class PaddleProduct
{
    public int Id { get; set; }

    /// <summary>Paddle product id, prefixed <c>pro_</c>.</summary>
    public required string PaddleProductId { get; set; }

    public required string Name { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; }
    public string? TaxCategory { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>Paddle status (e.g. <c>active</c>, <c>archived</c>).</summary>
    public required string Status { get; set; }

    /// <summary><c>service</c> or <c>addon</c>, derived from <c>custom_data.kind</c>.</summary>
    public string? Kind { get; set; }

    /// <summary>For <c>service</c> products: the in-app service this represents.</summary>
    public ServiceType? ServiceType { get; set; }

    /// <summary>For <c>addon</c> products: the service the addon attaches to.</summary>
    public ServiceType? ParentServiceType { get; set; }

    /// <summary>For <c>addon</c> products: stable code matching <see cref="Entities.ProductAddon.Code"/>.</summary>
    public string? AddonCode { get; set; }

    /// <summary>Raw <c>custom_data</c> JSON from Paddle, stored as <c>jsonb</c>.</summary>
    public string? CustomData { get; set; }

    public DateTimeOffset? PaddleCreatedAt { get; set; }
    public DateTimeOffset? PaddleUpdatedAt { get; set; }

    /// <summary>When the local mirror was last reconciled with Paddle.</summary>
    public DateTime LastSyncedAt { get; set; }

    public List<PaddlePrice> Prices { get; set; } = [];
}

public class PaddleProductConfiguration : IEntityTypeConfiguration<PaddleProduct>
{
    public void Configure(EntityTypeBuilder<PaddleProduct> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.PaddleProductId).IsRequired().HasMaxLength(64);
        builder.HasIndex(e => e.PaddleProductId).IsUnique();

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.Type).HasMaxLength(32);
        builder.Property(e => e.TaxCategory).HasMaxLength(64);
        builder.Property(e => e.ImageUrl).HasMaxLength(2048);
        builder.Property(e => e.Status).IsRequired().HasMaxLength(32);

        builder.Property(e => e.Kind).HasMaxLength(16);
        builder.HasIndex(e => e.Kind);

        builder.Property(e => e.ServiceType).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(e => e.ServiceType);

        builder.Property(e => e.ParentServiceType).HasConversion<string>().HasMaxLength(32);
        builder.HasIndex(e => e.ParentServiceType);

        builder.Property(e => e.AddonCode).HasMaxLength(64);
        builder.HasIndex(e => e.AddonCode);

        builder.Property(e => e.CustomData).HasColumnType("jsonb");
        builder.Property(e => e.LastSyncedAt).IsRequired();

        builder.HasMany(e => e.Prices)
            .WithOne(p => p.Product!)
            .HasForeignKey(p => p.PaddleProductInternalId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("paddle_products");
    }
}
