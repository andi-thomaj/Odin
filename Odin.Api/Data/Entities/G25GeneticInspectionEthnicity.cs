using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class G25GeneticInspectionEthnicity : BaseEntity
{
    public int Id { get; set; }
    public int G25GeneticInspectionId { get; set; }
    public G25GeneticInspection G25GeneticInspection { get; set; } = null!;
    public int G25EthnicityId { get; set; }
    public G25Ethnicity G25Ethnicity { get; set; } = null!;
}

public class G25GeneticInspectionEthnicityConfiguration : IEntityTypeConfiguration<G25GeneticInspectionEthnicity>
{
    public void Configure(EntityTypeBuilder<G25GeneticInspectionEthnicity> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.G25GeneticInspection)
            .WithMany(gi => gi.SelectedEthnicities)
            .HasForeignKey(e => e.G25GeneticInspectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.G25Ethnicity)
            .WithMany()
            .HasForeignKey(e => e.G25EthnicityId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.G25GeneticInspectionId, e.G25EthnicityId }).IsUnique();

        builder.ToTable("g25_genetic_inspection_ethnicities");
    }
}
