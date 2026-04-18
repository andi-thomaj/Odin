using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class G25GeneticInspectionContinent : BaseEntity
{
    public int Id { get; set; }
    public int G25GeneticInspectionId { get; set; }
    public G25GeneticInspection G25GeneticInspection { get; set; } = null!;
    public int G25ContinentId { get; set; }
    public G25Continent G25Continent { get; set; } = null!;
}

public class G25GeneticInspectionContinentConfiguration : IEntityTypeConfiguration<G25GeneticInspectionContinent>
{
    public void Configure(EntityTypeBuilder<G25GeneticInspectionContinent> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.G25GeneticInspection)
            .WithMany(gi => gi.SelectedContinents)
            .HasForeignKey(e => e.G25GeneticInspectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.G25Continent)
            .WithMany()
            .HasForeignKey(e => e.G25ContinentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.G25GeneticInspectionId, e.G25ContinentId }).IsUnique();

        builder.ToTable("g25_genetic_inspection_continents");
    }
}
