using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class QpadmRegion
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int EthnicityId { get; set; }
        public required QpadmEthnicity Ethnicity { get; set; }
    }

    public class QpadmRegionConfiguration : IEntityTypeConfiguration<QpadmRegion>
    {
        public void Configure(EntityTypeBuilder<QpadmRegion> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
            builder.ToTable("qpadm_regions");
        }
    }
}
