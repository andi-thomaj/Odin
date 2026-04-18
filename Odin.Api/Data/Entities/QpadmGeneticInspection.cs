using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities
{
    public class QpadmGeneticInspection : BaseEntity
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public Gender? Gender { get; set; }
        public List<QpadmGeneticInspectionRegion> GeneticInspectionRegions { get; set; } = [];
        public int RawGeneticFileId { get; set; }
        public RawGeneticFile? RawGeneticFile { get; set; }
        public byte[]? ProfilePicture { get; set; }
        public string? ProfilePictureFileName { get; set; }
        public QpadmResult? QpadmResult { get; set; }
        public int OrderId { get; set; }
        public QpadmOrder Order { get; set; }
    }

    public class QpadmGeneticInspectionConfiguration : IEntityTypeConfiguration<QpadmGeneticInspection>
    {
        public void Configure(EntityTypeBuilder<QpadmGeneticInspection> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            builder.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            builder.Property(e => e.Gender).HasConversion<string>().HasMaxLength(10);
            builder.Property(e => e.ProfilePictureFileName).HasMaxLength(255);

            builder.ToTable("qpadm_genetic_inspections");
        }
    }
}
