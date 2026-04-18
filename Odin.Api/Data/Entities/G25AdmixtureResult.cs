using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class G25AdmixtureResult : BaseEntity
{
    public int Id { get; set; }
    public int GeneticInspectionId { get; set; }
    public G25GeneticInspection GeneticInspection { get; set; } = null!;
    public double FitDistance { get; set; }
    public string ResultsVersion { get; set; } = string.Empty;
    public List<G25AdmixtureAncestor> Ancestors { get; set; } = [];
}

public class G25AdmixtureAncestor
{
    public string Name { get; set; } = string.Empty;
    public double Percentage { get; set; }
}

public class G25AdmixtureResultConfiguration : IEntityTypeConfiguration<G25AdmixtureResult>
{
    public void Configure(EntityTypeBuilder<G25AdmixtureResult> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.FitDistance).HasPrecision(18, 10);
        builder.Property(e => e.ResultsVersion).IsRequired().HasMaxLength(50);

        builder.HasOne(e => e.GeneticInspection)
            .WithMany(gi => gi.G25AdmixtureResults)
            .HasForeignKey(e => e.GeneticInspectionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(e => e.GeneticInspectionId).IsUnique();

        builder.OwnsMany(e => e.Ancestors, b =>
        {
            b.ToJson();
            b.Property(a => a.Name).IsRequired().HasMaxLength(500);
        });

        builder.ToTable("g25_admixture_results");
    }
}
