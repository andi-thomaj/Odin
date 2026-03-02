using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class QpadmResult : BaseEntity
    {
        public int Id { get; set; }
        public int GeneticInspectionId { get; set; }
        public GeneticInspection GeneticInspection { get; set; }
        public List<Population> Populations { get; set; } = [];
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

            builder.HasMany(e => e.Populations)
                .WithMany()
                .UsingEntity(j => j.ToTable("qpadm_result_populations"));

            builder.ToTable("qpadm_results");
        }
    }
}
