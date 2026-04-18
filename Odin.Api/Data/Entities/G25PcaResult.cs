using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class G25PcaResult : BaseEntity
{
    public int Id { get; set; }
    public int GeneticInspectionId { get; set; }
    public G25GeneticInspection GeneticInspection { get; set; } = null!;
    public int G25ContinentId { get; set; }
    public G25Continent G25Continent { get; set; } = null!;
    public string ResultsVersion { get; set; } = string.Empty;
    public List<G25PcaResultFile> PcaFiles { get; set; } = [];
}

public class G25PcaResultConfiguration : IEntityTypeConfiguration<G25PcaResult>
{
    public void Configure(EntityTypeBuilder<G25PcaResult> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.ResultsVersion).IsRequired().HasMaxLength(50);

        builder.HasOne(e => e.GeneticInspection)
            .WithMany(gi => gi.G25PcaResults)
            .HasForeignKey(e => e.GeneticInspectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.G25Continent)
            .WithMany()
            .HasForeignKey(e => e.G25ContinentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.GeneticInspectionId, e.G25ContinentId }).IsUnique();

        builder.ToTable("g25_pca_results");
    }
}
