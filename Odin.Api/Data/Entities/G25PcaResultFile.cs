using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

public class G25PcaResultFile : BaseEntity
{
    public int Id { get; set; }
    public int G25PcaResultId { get; set; }
    public G25PcaResult G25PcaResult { get; set; } = null!;
    public int G25PcaFileId { get; set; }
    public G25PcaFile G25PcaFile { get; set; } = null!;
}

public class G25PcaResultFileConfiguration : IEntityTypeConfiguration<G25PcaResultFile>
{
    public void Configure(EntityTypeBuilder<G25PcaResultFile> builder)
    {
        builder.HasKey(e => e.Id);

        builder.HasOne(e => e.G25PcaResult)
            .WithMany(r => r.PcaFiles)
            .HasForeignKey(e => e.G25PcaResultId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.G25PcaFile)
            .WithMany()
            .HasForeignKey(e => e.G25PcaFileId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.G25PcaResultId, e.G25PcaFileId }).IsUnique();

        builder.ToTable("g25_pca_result_files");
    }
}
