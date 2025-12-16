using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class RawGeneticFile
    {
        public int Id { get; set; }
        public required byte[] RawData { get; set; } = [];
        public required string FileName { get; set; }
    }

    public class RawGeneticFileConfiguration : IEntityTypeConfiguration<RawGeneticFile>
    {
        public void Configure(EntityTypeBuilder<RawGeneticFile> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.FileName).IsRequired().HasMaxLength(200);

            builder.ToTable("raw_genetic_files");
        }
    }
}
