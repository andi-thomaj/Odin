using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class G25Region : BaseEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int G25EthnicityId { get; set; }
        public G25Ethnicity G25Ethnicity { get; set; } = null!;
        public G25AdmixtureFile? AdmixtureFile { get; set; }
    }

    public class G25RegionConfiguration : IEntityTypeConfiguration<G25Region>
    {
        public void Configure(EntityTypeBuilder<G25Region> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);

            builder.HasOne(e => e.G25Ethnicity)
                .WithMany(e => e.G25Regions)
                .HasForeignKey(e => e.G25EthnicityId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => new { e.G25EthnicityId, e.Name }).IsUnique();

            builder.ToTable("g25_regions");
        }
    }
}
