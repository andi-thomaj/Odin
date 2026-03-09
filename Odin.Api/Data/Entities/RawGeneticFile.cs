using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class RawGeneticFile : BaseEntity
    {
        public int Id { get; set; }
        public required byte[] RawData { get; set; } = [];
        public required string FileName { get; set; }
        public bool IsDeleted { get; set; }
        public List<GeneticInspection> GeneticInspections { get; set; } = [];
    }

    public class RawGeneticFileConfiguration : IEntityTypeConfiguration<RawGeneticFile>
    {
        public void Configure(EntityTypeBuilder<RawGeneticFile> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.FileName).IsRequired().HasMaxLength(200);
            builder.Property(e => e.IsDeleted).HasDefaultValue(false);

            builder.HasQueryFilter(e => !e.IsDeleted);

            builder.HasMany(e => e.GeneticInspections)
                .WithOne(gi => gi.RawGeneticFile)
                .HasForeignKey(gi => gi.RawGeneticFileId);

            builder.ToTable("raw_genetic_files");
        }
    }
}
