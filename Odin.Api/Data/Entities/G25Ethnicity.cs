using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class G25Ethnicity : BaseEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int G25RegionId { get; set; }
        public G25Region G25Region { get; set; } = null!;
        public G25AdmixtureFile? AdmixtureFile { get; set; }
        public List<GeneticInspectionG25Ethnicity> GeneticInspectionG25Ethnicities { get; set; } = [];
    }

    public class G25EthnicityConfiguration : IEntityTypeConfiguration<G25Ethnicity>
    {
        public void Configure(EntityTypeBuilder<G25Ethnicity> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
            builder.HasIndex(e => e.Name).IsUnique();

            builder.HasOne(e => e.G25Region)
                .WithMany(r => r.Ethnicities)
                .HasForeignKey(e => e.G25RegionId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(e => e.G25RegionId);

            builder.ToTable("g25_ethnicities");
        }
    }
}
