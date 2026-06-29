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

            builder.HasOne(e => e.GeneticInspection)
                .WithOne(gi => gi.Order)
                .HasForeignKey<QpadmGeneticInspection>(gi => gi.OrderId)
                // Explicit (matches G25Order) — Cascade is already the DB default for this required FK, but the
                // refund-purge job RELIES on deleting the order cascading the inspection + results, so spell it out:
                // a future change (e.g. making OrderId optional) can't then silently break the cascade.
                .OnDelete(DeleteBehavior.Cascade);

            // Per-user order list filters on CreatedBy and orders by CreatedAt; the admin list orders
            // by CreatedAt across all rows. Without these the listings full-scan the table.
            builder.HasIndex(e => new { e.CreatedBy, e.CreatedAt });
            builder.HasIndex(e => e.CreatedAt);

            builder.ToTable("qpadm_orders");
        }
    }
}
