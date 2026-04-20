using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class G25DistancePopulationSample : BaseEntity
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Coordinates { get; set; } = string.Empty;
    public string Ids { get; set; } = string.Empty;
    public List<ResearchLink> ResearchLinks { get; set; } = [];

    public int? G25DistanceEraId { get; set; }
    public G25DistanceEra? G25DistanceEra { get; set; }
}

public class G25DistancePopulationSampleConfiguration : IEntityTypeConfiguration<G25DistancePopulationSample>
{
    public void Configure(EntityTypeBuilder<G25DistancePopulationSample> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Label).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Coordinates).IsRequired().HasColumnType("text");
        builder.Property(e => e.Ids).IsRequired().HasColumnType("text");
        builder.HasIndex(e => e.Label);

        builder.HasOne(e => e.G25DistanceEra)
            .WithMany(e => e.G25DistancePopulationSamples)
            .HasForeignKey(e => e.G25DistanceEraId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.G25DistanceEraId);

        builder.ToTable("g25_distance_population_samples");
    }
}
