using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class PopulationVideoFile : BaseEntity
    {
        public int Id { get; set; }
        public int PopulationId { get; set; }
        public Population Population { get; set; }
        public string FileName { get; set; }
        public byte[] FileData { get; set; }
        public string ContentType { get; set; }
        public long FileSizeBytes { get; set; }
    }

    public class PopulationVideoFileConfiguration : IEntityTypeConfiguration<PopulationVideoFile>
    {
        public void Configure(EntityTypeBuilder<PopulationVideoFile> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.FileName).IsRequired().HasMaxLength(200);
            builder.Property(e => e.FileData).IsRequired();
            builder.Property(e => e.ContentType).IsRequired().HasMaxLength(50);

            builder.HasOne(e => e.Population)
                .WithOne(p => p.PopulationVideoFile)
                .HasForeignKey<PopulationVideoFile>(e => e.PopulationId);

            builder.HasIndex(e => e.PopulationId).IsUnique();

            builder.ToTable("population_video_files");
        }
    }
}
