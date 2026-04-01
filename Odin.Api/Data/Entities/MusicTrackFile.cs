using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class MusicTrackFile : BaseEntity
    {
        public int Id { get; set; }
        public int MusicTrackId { get; set; }
        public MusicTrack MusicTrack { get; set; }
        public string FileName { get; set; }
        public byte[] FileData { get; set; }
        public string ContentType { get; set; }
        public long FileSizeBytes { get; set; }
    }

    public class MusicTrackFileConfiguration : IEntityTypeConfiguration<MusicTrackFile>
    {
        public void Configure(EntityTypeBuilder<MusicTrackFile> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.FileName).IsRequired().HasMaxLength(200);
            builder.Property(e => e.FileData).IsRequired();
            builder.Property(e => e.ContentType).IsRequired().HasMaxLength(50);

            builder.HasOne(e => e.MusicTrack)
                .WithOne(m => m.MusicTrackFile)
                .HasForeignKey<MusicTrackFile>(e => e.MusicTrackId);

            builder.HasIndex(e => e.MusicTrackId).IsUnique();

            builder.ToTable("music_track_files");
        }
    }
}
