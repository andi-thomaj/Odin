using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class G25DistanceFile : BaseEntity
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public required string Content { get; set; }
        public int G25DistanceEraId { get; set; }
        public G25DistanceEra DistanceEra { get; set; } = null!;
    }

    public class G25DistanceFileConfiguration : IEntityTypeConfiguration<G25DistanceFile>
    {
        public void Configure(EntityTypeBuilder<G25DistanceFile> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
            builder.HasIndex(e => e.Title).IsUnique();
            builder.Property(e => e.Content).IsRequired().HasColumnType("text");

            builder.HasOne(e => e.DistanceEra)
                .WithOne(er => er.DistanceFile)
                .HasForeignKey<G25DistanceFile>(e => e.G25DistanceEraId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.HasIndex(e => e.G25DistanceEraId).IsUnique();

            builder.ToTable("g25_distance_files");
        }
    }
}
