using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class G25DistanceFile : BaseEntity
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public required string Content { get; set; }
    }

    public class G25DistanceFileConfiguration : IEntityTypeConfiguration<G25DistanceFile>
    {
        public void Configure(EntityTypeBuilder<G25DistanceFile> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
            builder.HasIndex(e => e.Title).IsUnique();
            builder.Property(e => e.Content).IsRequired().HasColumnType("text");

            builder.ToTable("g25_distance_files");
        }
    }
}
