using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class QpadmResultEraGroup
    {
        public int Id { get; set; }
        public int QpadmResultId { get; set; }
        public QpadmResult QpadmResult { get; set; } = null!;
        public int EraId { get; set; }
        public Era Era { get; set; } = null!;
        public decimal PValue { get; set; }
        public string RightSources { get; set; } = string.Empty;
        public List<QpadmResultPopulation> QpadmResultPopulations { get; set; } = [];
    }

    public class QpadmResultEraGroupConfiguration : IEntityTypeConfiguration<QpadmResultEraGroup>
    {
        public void Configure(EntityTypeBuilder<QpadmResultEraGroup> builder)
        {
            builder.HasKey(e => e.Id);

            builder.Property(e => e.PValue).HasPrecision(3, 2);

            builder.HasOne(e => e.QpadmResult)
                .WithMany(e => e.QpadmResultEraGroups)
                .HasForeignKey(e => e.QpadmResultId);

            builder.HasOne(e => e.Era)
                .WithMany()
                .HasForeignKey(e => e.EraId);

            builder.ToTable("qpadm_result_era_groups");
        }
    }
}
