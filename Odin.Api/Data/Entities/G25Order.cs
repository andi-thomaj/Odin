using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities
{
    public class G25Order : BaseEntity
    {
        public int Id { get; set; }
        public decimal Price { get; set; }
        public OrderStatus Status { get; set; }
        public bool HasViewedResults { get; set; }
        public decimal DiscountAmount { get; set; }
        public bool ExpeditedProcessing { get; set; }
        public G25GeneticInspection GeneticInspection { get; set; }
    }

    public class G25OrderConfiguration : IEntityTypeConfiguration<G25Order>
    {
        public void Configure(EntityTypeBuilder<G25Order> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Price).IsRequired().HasPrecision(18, 2);
            builder.Property(e => e.Status).IsRequired().HasConversion<string>();
            builder.Property(e => e.HasViewedResults).IsRequired().HasDefaultValue(false);
            builder.Property(e => e.DiscountAmount).IsRequired().HasPrecision(18, 2).HasDefaultValue(0m);
            builder.Property(e => e.ExpeditedProcessing).IsRequired().HasDefaultValue(false);

            builder.HasOne(e => e.GeneticInspection)
                .WithOne(gi => gi.Order)
                .HasForeignKey<G25GeneticInspection>(gi => gi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.ToTable("g25_orders");
        }
    }
}
