using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class QpadmPopulationSample : BaseEntity
{
    public int Id { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Coordinates { get; set; } = string.Empty;
    public List<ResearchLink> ResearchLinks { get; set; } = [];
}

public class QpadmPopulationSampleConfiguration : IEntityTypeConfiguration<QpadmPopulationSample>
{
    public void Configure(EntityTypeBuilder<QpadmPopulationSample> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Label).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Coordinates).IsRequired().HasColumnType("text");
        builder.HasIndex(e => e.Label);

        builder.ToTable("qpadm_population_samples");
    }
}
