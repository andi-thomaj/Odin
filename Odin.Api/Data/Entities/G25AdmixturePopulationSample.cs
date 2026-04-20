using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class G25AdmixturePopulationSample : BaseEntity
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Coordinates { get; set; } = string.Empty;
    public string Ids { get; set; } = string.Empty;
    public List<ResearchLink> ResearchLinks { get; set; } = [];

    public int? G25AdmixtureEraId { get; set; }
    public G25AdmixtureEra? G25AdmixtureEra { get; set; }
}

public class G25AdmixturePopulationSampleConfiguration : IEntityTypeConfiguration<G25AdmixturePopulationSample>
{
    public void Configure(EntityTypeBuilder<G25AdmixturePopulationSample> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Label).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Coordinates).IsRequired().HasColumnType("text");
        builder.Property(e => e.Ids).IsRequired().HasColumnType("text");
        builder.HasIndex(e => e.Label);

        builder.HasOne(e => e.G25AdmixtureEra)
            .WithMany(e => e.G25AdmixturePopulationSamples)
            .HasForeignKey(e => e.G25AdmixtureEraId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(e => e.G25AdmixtureEraId);

        builder.ToTable("g25_admixture_population_samples");
    }
}
