using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class G25GeneticInspectionRegion : BaseEntity
{
    public int Id { get; set; }
    public int G25GeneticInspectionId { get; set; }
    public G25GeneticInspection G25GeneticInspection { get; set; } = null!;
    public int G25RegionId { get; set; }
    public G25Region G25Region { get; set; } = null!;
}

public class G25GeneticInspectionRegionConfiguration : IEntityTypeConfiguration<G25GeneticInspectionRegion>
{
    public void Configure(EntityTypeBuilder<G25GeneticInspectionRegion> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.G25GeneticInspection)
            .WithMany(gi => gi.SelectedRegions)
            .HasForeignKey(e => e.G25GeneticInspectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.G25Region)
            .WithMany()
            .HasForeignKey(e => e.G25RegionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.G25GeneticInspectionId, e.G25RegionId }).IsUnique();

        builder.ToTable("g25_genetic_inspection_regions");
    }
}
