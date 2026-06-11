using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

/// <summary>
/// Many-to-many link between a <see cref="QpadmPopulation"/> and a merge-panel sample. The sample
/// lives in the panel's <c>.ind</c> file (served by tools-api), not in this database, so it is
/// referenced by its stable <see cref="SampleId"/> (e.g. <c>HO.001</c>) plus the owning
/// <see cref="Panel"/> rather than by a foreign key. One population links many samples and one
/// sample links many populations.
/// </summary>
public class QpadmPopulationPanelSample : BaseEntity
{
    public int Id { get; set; }
    public int QpadmPopulationId { get; set; }
    public QpadmPopulation Population { get; set; } = null!;

    /// <summary>Merge panel the sample belongs to (e.g. <c>HO</c>).</summary>
    public string Panel { get; set; } = string.Empty;

    /// <summary>Stable sample identifier from the panel's <c>.ind</c> (column 1, e.g. <c>HO.001</c>).</summary>
    public string SampleId { get; set; } = string.Empty;
}

public class QpadmPopulationPanelSampleConfiguration : IEntityTypeConfiguration<QpadmPopulationPanelSample>
{
    public void Configure(EntityTypeBuilder<QpadmPopulationPanelSample> builder)
    {
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Panel).IsRequired().HasMaxLength(50);
        builder.Property(e => e.SampleId).IsRequired().HasMaxLength(200);

        builder.HasOne(e => e.Population)
            .WithMany(p => p.PanelSamples)
            .HasForeignKey(e => e.QpadmPopulationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => new { e.QpadmPopulationId, e.Panel, e.SampleId }).IsUnique();
        builder.HasIndex(e => new { e.Panel, e.SampleId });

        builder.ToTable("qpadm_population_panel_samples");
    }
}
