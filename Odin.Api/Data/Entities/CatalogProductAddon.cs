using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class CatalogProductAddon
{
    public int CatalogProductId { get; set; }
    public CatalogProduct CatalogProduct { get; set; } = null!;
    public int ProductAddonId { get; set; }
    public ProductAddon ProductAddon { get; set; } = null!;
}

public class CatalogProductAddonConfiguration : IEntityTypeConfiguration<CatalogProductAddon>
{
    public void Configure(EntityTypeBuilder<CatalogProductAddon> builder)
    {
        builder.HasKey(e => new { e.CatalogProductId, e.ProductAddonId });

        builder.HasOne(e => e.CatalogProduct)
            .WithMany(p => p.CatalogProductAddons)
            .HasForeignKey(e => e.CatalogProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ProductAddon)
            .WithMany(a => a.CatalogProductAddons)
            .HasForeignKey(e => e.ProductAddonId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.ToTable("catalog_product_addons");
    }
}
