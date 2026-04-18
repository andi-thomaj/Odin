using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class G25AdmixtureFile : BaseEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Content { get; set; }
        public int G25RegionId { get; set; }
        public G25Region G25Region { get; set; } = null!;
    }

    public class G25AdmixtureFileConfiguration : IEntityTypeConfiguration<G25AdmixtureFile>
    {
        public void Configure(EntityTypeBuilder<G25AdmixtureFile> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
            builder.Property(e => e.Content).IsRequired().HasColumnType("text");

            builder.HasOne(e => e.G25Region)
                .WithOne(r => r.AdmixtureFile)
                .HasForeignKey<G25AdmixtureFile>(e => e.G25RegionId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => e.G25RegionId).IsUnique();

            builder.ToTable("g25_admixture_files");
        }
    }
}
