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

        /// <summary>
        /// Cache-bust marker for the population's keyframe WEBP in R2 at
        /// <c>qpAdm/population-videos/{slug}.webp</c>. <c>null</c> means no keyframe published; otherwise
        /// bumped on every upload (<see cref="DateTime.UtcNow"/> ticks) and used as <c>?v=</c> on the URL.
        /// Mirrors <see cref="VideoAvatarVersion"/> for the still keyframe.
        /// </summary>
        public long? KeyframeVersion { get; set; }

        /// <summary>
        /// Admin override for the keyframe (image) generation prompt fed to the media service.
        /// <c>null</c> means "use the frontend's composed default template" (see
        /// <c>buildAvatarImagePrompt</c> in <c>odin-react/.../ancestry-video-media/manifest.ts</c>) —
        /// so improving that template applies to every non-customized population automatically.
        /// </summary>
        public string? ImagePrompt { get; set; }

        /// <summary>Admin override for the video generation prompt. <c>null</c> ⇒ frontend default template.</summary>
        public string? VideoPrompt { get; set; }

        /// <summary>
        /// Higgsfield video model id used to (re)generate the avatar loop — <c>seedance_2_0</c> or
        /// <c>kling3_0</c>. <c>null</c> ⇒ the frontend default (Kling 3.0).
        /// </summary>
        public string? VideoModel { get; set; }

        /// <summary>
        /// Video model "mode" (model-dependent: Kling <c>pro|std|4k</c>, Seedance <c>std|fast</c>).
        /// <c>null</c> ⇒ the model's default mode.
        /// </summary>
        public string? VideoMode { get; set; }

        /// <summary>Video clip duration in seconds (5 or 10). <c>null</c> ⇒ the default (5).</summary>
        public int? VideoDurationSeconds { get; set; }

        /// <summary>
        /// Merge-panel samples (by stable <c>.ind</c> sample id) linked to this population.
        /// Many-to-many — see <see cref="QpadmPopulationPanelSample"/>.
        /// </summary>
        public List<QpadmPopulationPanelSample> PanelSamples { get; set; } = [];
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
            builder.Property(e => e.ImagePrompt).HasMaxLength(4000);
            builder.Property(e => e.VideoPrompt).HasMaxLength(4000);
            builder.Property(e => e.VideoModel).HasMaxLength(50);
            builder.Property(e => e.VideoMode).HasMaxLength(20);
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
