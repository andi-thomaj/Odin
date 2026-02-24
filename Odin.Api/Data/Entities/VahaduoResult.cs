using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class VahaduoResult
    {
        public int Id { get; set; }
        public int GeneticInspectionId { get; set; }
        public GeneticInspection GeneticInspection { get; set; }
    }

    public class VahaduoResultConfiguration : IEntityTypeConfiguration<VahaduoResult>
    {
        public void Configure(EntityTypeBuilder<VahaduoResult> builder)
        {
            builder.HasKey(e => e.Id);

            builder.HasOne(e => e.GeneticInspection)
                .WithOne(e => e.VahaduoResult)
                .HasForeignKey<VahaduoResult>(e => e.GeneticInspectionId);

            builder.ToTable("vahaduo_results");
        }
    }
}
