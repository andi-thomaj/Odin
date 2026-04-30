using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities
{
    public class QpadmOrder : BaseEntity
    {
        public int Id { get; set; }
        public decimal Price { get; set; }
        public OrderStatus Status { get; set; }
        public bool HasViewedResults { get; set; }
        public decimal DiscountAmount { get; set; }
        public bool ExpeditedProcessing { get; set; }
        public bool IncludesYHaplogroup { get; set; }
        public bool IncludesRawMerge { get; set; }
        public QpadmGeneticInspection GeneticInspection { get; set; }

        /// <summary>
        /// Snapshot of which addons were on this order, captured at order creation time.
        /// Stored as <c>jsonb</c>; shape is <c>[{ paddleProductId, addonCode, displayName, unitPriceSnapshot }]</c>.
        /// We snapshot rather than join so historical orders keep their original prices/names
        /// even when the underlying Paddle product is renamed or repriced.
        /// </summary>
        public string? AddonsJson { get; set; }
    }

    public class QpadmOrderConfiguration : IEntityTypeConfiguration<QpadmOrder>
    {
        public void Configure(EntityTypeBuilder<QpadmOrder> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Price).IsRequired().HasPrecision(18, 2);
            builder.Property(e => e.Status).IsRequired().HasConversion<string>();
            builder.Property(e => e.HasViewedResults).IsRequired().HasDefaultValue(false);
            builder.Property(e => e.DiscountAmount).IsRequired().HasPrecision(18, 2).HasDefaultValue(0m);
            builder.Property(e => e.ExpeditedProcessing).IsRequired().HasDefaultValue(false);
            builder.Property(e => e.IncludesYHaplogroup).IsRequired().HasDefaultValue(false);
            builder.Property(e => e.IncludesRawMerge).IsRequired().HasDefaultValue(false);

            builder.Property(e => e.AddonsJson).HasColumnType("jsonb");

            builder.HasOne(e => e.GeneticInspection)
                .WithOne(gi => gi.Order)
                .HasForeignKey<QpadmGeneticInspection>(gi => gi.OrderId);

            builder.ToTable("qpadm_orders");
        }
    }
}
