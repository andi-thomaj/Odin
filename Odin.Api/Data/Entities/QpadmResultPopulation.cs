using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class QpadmResultPopulation
    {
        public int QpadmResultEraGroupId { get; set; }
        public QpadmResultEraGroup QpadmResultEraGroup { get; set; } = null!;
        public int PopulationId { get; set; }
        public QpadmPopulation Population { get; set; } = null!;
        public decimal Percentage { get; set; }
        public decimal StandardError { get; set; }
        public decimal ZScore { get; set; }
    }

    public class QpadmResultPopulationConfiguration : IEntityTypeConfiguration<QpadmResultPopulation>
    {
        public void Configure(EntityTypeBuilder<QpadmResultPopulation> builder)
        {
            builder.HasKey(e => new { e.QpadmResultEraGroupId, e.PopulationId });

            builder.Property(e => e.Percentage).HasPrecision(5, 2);
            builder.Property(e => e.StandardError).HasPrecision(5, 4);
            builder.Property(e => e.ZScore).HasPrecision(5, 4);

            builder.HasOne(e => e.QpadmResultEraGroup)
                .WithMany(e => e.QpadmResultPopulations)
                .HasForeignKey(e => e.QpadmResultEraGroupId);

            builder.HasOne(e => e.Population)
                .WithMany()
                .HasForeignKey(e => e.PopulationId);

            builder.ToTable("qpadm_result_populations");
        }
    }
}
