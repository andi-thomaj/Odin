using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class ResearchLink : BaseEntity
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;

    public int? G25PopulationSampleId { get; set; }
    public G25PopulationSample? G25PopulationSample { get; set; }

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

        builder.HasOne(e => e.G25PopulationSample)
            .WithMany(e => e.ResearchLinks)
            .HasForeignKey(e => e.G25PopulationSampleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.QpadmPopulationSample)
            .WithMany(e => e.ResearchLinks)
            .HasForeignKey(e => e.QpadmPopulationSampleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.G25PopulationSampleId);
        builder.HasIndex(e => e.QpadmPopulationSampleId);

        builder.ToTable("research_links");
    }
}
