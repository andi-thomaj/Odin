using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities;

public class CatalogProduct
{
    public int Id { get; set; }
    /// <summary>Aligns with <see cref="Order.Service"/> (same persisted string as <c>Odin.Api.Data.Enums.OrderService</c>, e.g. qpAdm).</summary>
    public OrderService ServiceType { get; set; }
    public required string DisplayName { get; set; }
    public string? Description { get; set; }
    public decimal BasePrice { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CatalogProductAddon> CatalogProductAddons { get; set; } = [];
}

public class CatalogProductConfiguration : IEntityTypeConfiguration<CatalogProduct>
{
    public void Configure(EntityTypeBuilder<CatalogProduct> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ServiceType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Description).HasMaxLength(2000);
        builder.Property(e => e.BasePrice).IsRequired().HasPrecision(18, 2);
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
        builder.HasIndex(e => e.ServiceType).IsUnique();

        builder.ToTable("catalog_products");
    }
}
