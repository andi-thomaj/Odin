using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class Population : BaseEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string GeoJson { get; set; }
        public string IconFileName { get; set; }
        public int EraId { get; set; }
        public Era Era { get; set; }
        public int MusicTrackId { get; set; }
        public MusicTrack MusicTrack { get; set; }
    }

    public class PopulationConfiguration : IEntityTypeConfiguration<Population>
    {
        public void Configure(EntityTypeBuilder<Population> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
            builder.Property(e => e.Description).IsRequired().HasMaxLength(2000);
            builder.Property(e => e.GeoJson).IsRequired();
            builder.Property(e => e.IconFileName).IsRequired().HasMaxLength(200);
            builder.HasOne(e => e.Era)
                .WithMany(e => e.Populations)
                .HasForeignKey(e => e.EraId);
            builder.HasOne(e => e.MusicTrack)
                .WithMany(e => e.Populations)
                .HasForeignKey(e => e.MusicTrackId);

            builder.ToTable("populations");
        }
    }
}
