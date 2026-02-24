using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class QpadmResult
    {
        public int Id { get; set; }
        public int GeneticInspectionId { get; set; }
        public GeneticInspection GeneticInspection { get; set; }
        public List<TimeEraSubEra> TimeEraSubEras { get; set; } = [];
        public decimal Weight { get; set; }
        public decimal StandardError { get; set; }
        public decimal ZScore { get; set; }
        public decimal PiValue { get; set; }
        public string RightSources { get; set; }
        public string LeftSources { get; set; }
        public List<QpadmResultResearchLink> QpadmResultResearchLinks { get; set; } = [];
    }

    public class QpadmResultConfiguration : IEntityTypeConfiguration<QpadmResult>
    {
        public void Configure(EntityTypeBuilder<QpadmResult> builder)
        {
            builder.HasKey(e => e.Id);

            builder.HasOne(e => e.GeneticInspection)
                .WithOne(e => e.QpadmResult)
                .HasForeignKey<QpadmResult>(e => e.GeneticInspectionId);

            builder.HasMany(e => e.TimeEraSubEras)
                .WithMany()
                .UsingEntity(j => j.ToTable("qpadm_result_time_era_sub_eras"));

            builder.ToTable("qpadm_results");
        }
    }
}
