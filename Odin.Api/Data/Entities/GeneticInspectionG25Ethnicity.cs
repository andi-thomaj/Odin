using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class GeneticInspectionG25Ethnicity
    {
        public int GeneticInspectionId { get; set; }
        public required GeneticInspection GeneticInspection { get; set; }
        public int G25EthnicityId { get; set; }
        public required G25Ethnicity G25Ethnicity { get; set; }
    }

    public class GeneticInspectionG25EthnicityConfiguration : IEntityTypeConfiguration<GeneticInspectionG25Ethnicity>
    {
        public void Configure(EntityTypeBuilder<GeneticInspectionG25Ethnicity> builder)
        {
            builder.HasKey(e => new { e.GeneticInspectionId, e.G25EthnicityId });
            builder.HasOne(e => e.GeneticInspection)
                .WithMany(e => e.GeneticInspectionG25Ethnicities)
                .HasForeignKey(e => e.GeneticInspectionId);
            builder.HasOne(e => e.G25Ethnicity)
                .WithMany(e => e.GeneticInspectionG25Ethnicities)
                .HasForeignKey(e => e.G25EthnicityId);

            builder.ToTable("genetic_inspection_g25_ethnicities");
        }
    }
}
