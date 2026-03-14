using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities
{
    public class Order : BaseEntity
    {
        public int Id { get; set; }
        public decimal Price { get; set; }
        public OrderService Service { get; set; }
        public OrderStatus Status { get; set; }
        public bool HasViewedResults { get; set; }
        public GeneticInspection GeneticInspection { get; set; }
    }

    public class OrderConfiguration : IEntityTypeConfiguration<Order>
    {
        public void Configure(EntityTypeBuilder<Order> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Price).IsRequired().HasPrecision(18, 2);
            builder.Property(e => e.Service).IsRequired().HasConversion<string>();
            builder.Property(e => e.Status).IsRequired().HasConversion<string>();
            builder.Property(e => e.HasViewedResults).IsRequired().HasDefaultValue(false);

            builder.HasOne(e => e.GeneticInspection)
                .WithOne(gi => gi.Order)
                .HasForeignKey<GeneticInspection>(gi => gi.OrderId);

            builder.ToTable("orders");
        }
    }
}
