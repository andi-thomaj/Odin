using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class RawGeneticFile : BaseEntity
    {
        public int Id { get; set; }
        public required byte[] RawData { get; set; } = [];
        public required string RawDataFileName { get; set; }
        public byte[]? MergedRawData { get; set; }
        public string? MergedRawDataFileName { get; set; }
        public bool IsDeleted { get; set; }
        public List<QpadmGeneticInspection> GeneticInspections { get; set; } = [];
    }

    public class RawGeneticFileConfiguration : IEntityTypeConfiguration<RawGeneticFile>
    {
        public void Configure(EntityTypeBuilder<RawGeneticFile> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.RawDataFileName).IsRequired().HasMaxLength(200);
            builder.Property(e => e.MergedRawDataFileName).HasMaxLength(200);
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);

            builder.HasQueryFilter(e => !e.IsDeleted);

            builder.HasMany(e => e.GeneticInspections)
                .WithOne(gi => gi.RawGeneticFile)
                .HasForeignKey(gi => gi.RawGeneticFileId);

            // Per-user uniqueness: a user cannot have two active uploads with the same
            // file name. Soft-deleted rows are excluded so a user can re-upload the same
            // name after deleting it.
            builder.HasIndex(e => new { e.CreatedBy, e.RawDataFileName })
                .IsUnique()
                .HasFilter("\"IsDeleted\" = false");

            builder.ToTable("raw_genetic_files");
        }
    }
}
