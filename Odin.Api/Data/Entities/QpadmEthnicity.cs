using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class QpadmEthnicity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public List<QpadmRegion> Regions { get; set; } = [];
    }

    public class QpadmEthnicityConfiguration : IEntityTypeConfiguration<QpadmEthnicity>
    {
        public void Configure(EntityTypeBuilder<QpadmEthnicity> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);

            builder.ToTable("qpadm_ethnicities");
        }
    }
}
