using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities
{
    public class G25GeneticInspection
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
        public string FirstName { get; set; }
        public string MiddleName { get; set; }
        public string LastName { get; set; }
        public Gender? Gender { get; set; }
        public string? G25Coordinates { get; set; }
        public int RawGeneticFileId { get; set; }
        public RawGeneticFile? RawGeneticFile { get; set; }
        public byte[]? ProfilePicture { get; set; }
        public string? ProfilePictureFileName { get; set; }
        public List<G25GeneticInspectionEthnicity> SelectedEthnicities { get; set; } = [];
        public List<G25GeneticInspectionRegion> SelectedRegions { get; set; } = [];
        public List<G25GeneticInspectionContinent> SelectedContinents { get; set; } = [];
        public List<G25DistanceResult> G25DistanceResults { get; set; } = [];
        public List<G25AdmixtureResult> G25AdmixtureResults { get; set; } = [];
        public List<G25PcaResult> G25PcaResults { get; set; } = [];
        public int OrderId { get; set; }
        public G25Order Order { get; set; }
    }

    public class G25GeneticInspectionConfiguration : IEntityTypeConfiguration<G25GeneticInspection>
    {
        public void Configure(EntityTypeBuilder<G25GeneticInspection> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.FirstName).IsRequired().HasMaxLength(100);
            builder.Property(e => e.LastName).IsRequired().HasMaxLength(100);
            builder.Property(e => e.MiddleName).HasMaxLength(100);
            builder.Property(e => e.Gender).HasConversion<string>().HasMaxLength(10);
            builder.Property(e => e.G25Coordinates).HasMaxLength(500);
            builder.Property(e => e.ProfilePictureFileName).HasMaxLength(255);

            builder.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.RawGeneticFile)
                .WithMany()
                .HasForeignKey(e => e.RawGeneticFileId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.ToTable("g25_genetic_inspections");
        }
    }
}
