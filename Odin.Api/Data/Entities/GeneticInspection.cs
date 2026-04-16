using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities
{
    public class GeneticInspection : BaseEntity
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public string? PaternalHaplogroup { get; set; }
        public Gender? Gender { get; set; }
        public string? G25Coordinates { get; set; }
        public List<GeneticInspectionRegion> GeneticInspectionRegions { get; set; } = [];
        public List<GeneticInspectionG25Ethnicity> GeneticInspectionG25Ethnicities { get; set; } = [];
        public int RawGeneticFileId { get; set; }
        public RawGeneticFile? RawGeneticFile { get; set; }
        public byte[]? ProfilePicture { get; set; }
        public string? ProfilePictureFileName { get; set; }
        public QpadmResult? QpadmResult { get; set; }
        public int OrderId { get; set; }
        public Order Order { get; set; }
    }

    public class GeneticInspectionConfiguration : IEntityTypeConfiguration<GeneticInspection>
    {
        public void Configure(EntityTypeBuilder<GeneticInspection> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            builder.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            builder.Property(e => e.PaternalHaplogroup).HasMaxLength(50);
            builder.Property(e => e.Gender).HasConversion<string>().HasMaxLength(10);
            builder.Property(e => e.G25Coordinates).HasMaxLength(500);
            builder.Property(e => e.ProfilePictureFileName).HasMaxLength(255);

            builder.ToTable("genetic_inspections");
        }
    }
}
