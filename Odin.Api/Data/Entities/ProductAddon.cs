using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class ProductAddon
{
    public int Id { get; set; }
    /// <summary>Stable code for fulfillment (e.g. EXPEDITED, Y_HAPLOGROUP).</summary>
    public required string Code { get; set; }
    public required string DisplayName { get; set; }
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public List<CatalogProductAddon> CatalogProductAddons { get; set; } = [];
    public List<OrderLineAddon> OrderLineAddons { get; set; } = [];
}

public class ProductAddonConfiguration : IEntityTypeConfiguration<ProductAddon>
{
    public void Configure(EntityTypeBuilder<ProductAddon> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Code).IsRequired().HasMaxLength(64);
        builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Price).IsRequired().HasPrecision(18, 2);
        builder.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
        builder.HasIndex(e => e.Code).IsUnique();

        builder.ToTable("product_addons");
    }
}
