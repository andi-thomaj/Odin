using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class QpadmGeneticInspectionRegion
    {
        public int GeneticInspectionId { get; set; }
        public required QpadmGeneticInspection GeneticInspection { get; set; }
        public int RegionId { get; set; }
        public required QpadmRegion Region { get; set; }
    }

    public class QpadmGeneticInspectionRegionConfiguration : IEntityTypeConfiguration<QpadmGeneticInspectionRegion>
    {
        public void Configure(EntityTypeBuilder<QpadmGeneticInspectionRegion> builder)
        {
            builder.HasKey(e => new { e.GeneticInspectionId, e.RegionId });
            builder.HasOne(e => e.GeneticInspection)
                .WithMany(e => e.GeneticInspectionRegions)
                .HasForeignKey(e => e.GeneticInspectionId);
            builder.HasOne(e => e.Region)
                .WithMany()
                .HasForeignKey(e => e.RegionId);

            builder.ToTable("qpadm_genetic_inspection_regions");
        }
    }
}
