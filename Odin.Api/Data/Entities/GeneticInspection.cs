using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class GeneticInspection
    {
        public int UserId { get; set; }
        public User User { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public List<GeneticInspectionRegion> GeneticInspectionRegions { get; set; } = [];
        public List<Country> Countries { get; set; } = [];
        public int RawGeneticFileId { get; set; }
        public RawGeneticFile RawGeneticFile { get; set; }
    }

    public class GeneticInspectionConfiguration : IEntityTypeConfiguration<GeneticInspection>
    {
        public void Configure(EntityTypeBuilder<GeneticInspection> builder)
        {
            builder.HasKey(e => e.UserId);
            builder.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            builder.Property(e => e.LastName).IsRequired().HasMaxLength(100);

            builder.ToTable("genetic_inspections");
        }
    }
}
