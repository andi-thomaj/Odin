using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class QpadmPopulation : BaseEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string GeoJson { get; set; }
        public string IconFileName { get; set; }
        public string Color { get; set; }
        public int EraId { get; set; }
        public QpadmEra Era { get; set; }
        public int MusicTrackId { get; set; }
        public MusicTrack MusicTrack { get; set; }
        /// <summary>
        /// Cache-bust marker for the population's MP4 in R2 at <c>qpAdm/population-videos/{Id}.mp4</c>.
        /// <c>null</c> means no avatar uploaded; otherwise the value is bumped on every upload
        /// (typically <see cref="DateTime.UtcNow"/> ticks) and used as <c>?v=</c> on the public URL.
        /// </summary>
        public long? VideoAvatarVersion { get; set; }
    }

    public class QpadmPopulationConfiguration : IEntityTypeConfiguration<QpadmPopulation>
    {
        public void Configure(EntityTypeBuilder<QpadmPopulation> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
            builder.Property(e => e.Description).IsRequired().HasMaxLength(2000);
            builder.Property(e => e.GeoJson).IsRequired();
            builder.Property(e => e.IconFileName).IsRequired().HasMaxLength(200);
            builder.Property(e => e.Color).IsRequired().HasMaxLength(7);
            builder.HasOne(e => e.Era)
                .WithMany(e => e.Populations)
                .HasForeignKey(e => e.EraId);
            builder.HasOne(e => e.MusicTrack)
                .WithMany(e => e.Populations)
                .HasForeignKey(e => e.MusicTrackId);

            builder.ToTable("qpadm_populations");
        }
    }
}
