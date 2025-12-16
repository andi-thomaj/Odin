using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class GeneticInspectionRegion
    {
        public int GeneticInspectionId { get; set; }
        public required GeneticInspection GeneticInspection { get; set; }
        public int RegionId { get; set; }
        public required Region Region { get; set; }
    }

    public class GeneticInspectionRegionConfiguration : IEntityTypeConfiguration<GeneticInspectionRegion>
    {
        public void Configure(EntityTypeBuilder<GeneticInspectionRegion> builder)
        {
            builder.HasKey(e => new { e.GeneticInspectionId, e.RegionId });
            builder.HasOne(e => e.GeneticInspection)
                .WithMany(e => e.GeneticInspectionRegions)
                .HasForeignKey(e => e.GeneticInspectionId);
            builder.HasOne(e => e.Region)
                .WithMany()
                .HasForeignKey(e => e.RegionId);

            builder.ToTable("genetic_inspection_regions");
        }
    }
}
