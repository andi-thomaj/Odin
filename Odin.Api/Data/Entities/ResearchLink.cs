using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class ResearchLink : BaseEntity
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;

    public int? G25AdmixturePopulationSampleId { get; set; }
    public G25AdmixturePopulationSample? G25AdmixturePopulationSample { get; set; }

    public int? G25DistancePopulationSampleId { get; set; }
    public G25DistancePopulationSample? G25DistancePopulationSample { get; set; }

    public int? G25PcaPopulationsSampleId { get; set; }
    public G25PcaPopulationsSample? G25PcaPopulationsSample { get; set; }

    public int? QpadmPopulationSampleId { get; set; }
    public QpadmPopulationSample? QpadmPopulationSample { get; set; }
}

public class ResearchLinkConfiguration : IEntityTypeConfiguration<ResearchLink>
{
    public void Configure(EntityTypeBuilder<ResearchLink> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Label).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Link).IsRequired().HasMaxLength(2000);

        builder.HasOne(e => e.G25AdmixturePopulationSample)
            .WithMany(e => e.ResearchLinks)
            .HasForeignKey(e => e.G25AdmixturePopulationSampleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.G25DistancePopulationSample)
            .WithMany(e => e.ResearchLinks)
            .HasForeignKey(e => e.G25DistancePopulationSampleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.G25PcaPopulationsSample)
            .WithMany(e => e.ResearchLinks)
            .HasForeignKey(e => e.G25PcaPopulationsSampleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.QpadmPopulationSample)
            .WithMany(e => e.ResearchLinks)
            .HasForeignKey(e => e.QpadmPopulationSampleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.G25AdmixturePopulationSampleId);
        builder.HasIndex(e => e.G25DistancePopulationSampleId);
        builder.HasIndex(e => e.G25PcaPopulationsSampleId);
        builder.HasIndex(e => e.QpadmPopulationSampleId);

        builder.ToTable("research_links");
    }
}
