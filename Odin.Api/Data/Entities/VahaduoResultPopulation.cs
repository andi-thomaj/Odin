using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class VahaduoResultPopulation
    {
        public int VahaduoResultId { get; set; }
        public VahaduoResult VahaduoResult { get; set; } = null!;
        public int PopulationId { get; set; }
        public Population Population { get; set; } = null!;
        public decimal Distance { get; set; }
    }

    public class VahaduoResultPopulationConfiguration : IEntityTypeConfiguration<VahaduoResultPopulation>
    {
        public void Configure(EntityTypeBuilder<VahaduoResultPopulation> builder)
        {
            builder.HasKey(e => new { e.VahaduoResultId, e.PopulationId });

            builder.Property(e => e.Distance).HasPrecision(18, 8);

            builder.HasOne(e => e.VahaduoResult)
                .WithMany(e => e.VahaduoResultPopulations)
                .HasForeignKey(e => e.VahaduoResultId);

            builder.HasOne(e => e.Population)
                .WithMany()
                .HasForeignKey(e => e.PopulationId);

            builder.ToTable("vahaduo_result_populations");
        }
    }
}
