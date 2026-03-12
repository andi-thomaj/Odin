using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class QpadmResult : BaseEntity
    {
        public int Id { get; set; }
        public int GeneticInspectionId { get; set; }
        public GeneticInspection GeneticInspection { get; set; }
        public List<QpadmResultEraGroup> QpadmResultEraGroups { get; set; } = [];
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

            builder.ToTable("qpadm_results");
        }
    }
}
