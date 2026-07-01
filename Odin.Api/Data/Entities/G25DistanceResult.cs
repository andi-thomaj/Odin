using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class G25DistanceResult : BaseEntity
{
    public int Id { get; set; }
    public int GeneticInspectionId { get; set; }
    public G25GeneticInspection GeneticInspection { get; set; }
    public int G25DistanceEraId { get; set; }
    public G25DistanceEra DistanceEra { get; set; }
    public string ResultsVersion { get; set; } = string.Empty;
    public List<G25DistancePopulation> Populations { get; set; } = [];
}

public class G25DistancePopulation
{
    public string Name { get; set; } = string.Empty;
    public double Distance { get; set; }
    public int Rank { get; set; }
}

public class G25DistanceResultConfiguration : IEntityTypeConfiguration<G25DistanceResult>
{
    public void Configure(EntityTypeBuilder<G25DistanceResult> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ResultsVersion).IsRequired().HasMaxLength(50);

        builder.HasOne(e => e.GeneticInspection)
            .WithMany(gi => gi.G25DistanceResults)
            .HasForeignKey(e => e.GeneticInspectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.DistanceEra)
            .WithMany()
            .HasForeignKey(e => e.G25DistanceEraId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.GeneticInspectionId, e.G25DistanceEraId }).IsUnique();

        builder.OwnsMany(e => e.Populations, b =>
        {
            b.ToJson();
            b.Property(p => p.Name).IsRequired().HasMaxLength(500);
        });

        builder.ToTable("g25_distance_results");
    }
}
