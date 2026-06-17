using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class QpadmResult : BaseEntity, IAppScoped
    {
        public int Id { get; set; }
        /// <summary>Owning application (applications.key). Set from the parent inspection when computed in a
        /// background job; query-filtered — see <see cref="IAppScoped"/>.</summary>
        public string App { get; set; } = string.Empty;
        public int GeneticInspectionId { get; set; }
        public QpadmGeneticInspection GeneticInspection { get; set; }
        public string ResultsVersion { get; set; } = string.Empty;
        public List<QpadmResultEraGroup> QpadmResultEraGroups { get; set; } = [];
    }

    public class QpadmResultConfiguration : IEntityTypeConfiguration<QpadmResult>
    {
        public void Configure(EntityTypeBuilder<QpadmResult> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.ResultsVersion).IsRequired().HasMaxLength(50);

            builder.HasOne(e => e.GeneticInspection)
                .WithOne(e => e.QpadmResult)
                .HasForeignKey<QpadmResult>(e => e.GeneticInspectionId);

            builder.Property(e => e.App).IsRequired().HasMaxLength(50);

            builder.ToTable("qpadm_results");
        }
    }
}
