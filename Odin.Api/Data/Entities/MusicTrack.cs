using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class MusicTrack : BaseEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string FileName { get; set; }
        public int DisplayOrder { get; set; }
        /// <summary>Binary audio file data. CAUTION: Never Include in bulk queries — contains large binary data.</summary>
        public MusicTrackFile? MusicTrackFile { get; set; }
        public ICollection<QpadmPopulation> Populations { get; set; } = [];
    }

    public class MusicTrackConfiguration : IEntityTypeConfiguration<MusicTrack>
    {
        public void Configure(EntityTypeBuilder<MusicTrack> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
            builder.Property(e => e.FileName).IsRequired().HasMaxLength(200);

            builder.ToTable("music_tracks");
        }
    }
}
