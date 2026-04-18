using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class OrderLineAddon
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public QpadmOrder Order { get; set; } = null!;
    public int ProductAddonId { get; set; }
    public ProductAddon ProductAddon { get; set; } = null!;
    public decimal UnitPriceSnapshot { get; set; }
}

public class OrderLineAddonConfiguration : IEntityTypeConfiguration<OrderLineAddon>
{
    public void Configure(EntityTypeBuilder<OrderLineAddon> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.UnitPriceSnapshot).IsRequired().HasPrecision(18, 2);

        builder.HasOne(e => e.Order)
            .WithMany(o => o.OrderLineAddons)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.ProductAddon)
            .WithMany(a => a.OrderLineAddons)
            .HasForeignKey(e => e.ProductAddonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.OrderId);

        builder.ToTable("order_line_addons");
    }
}
