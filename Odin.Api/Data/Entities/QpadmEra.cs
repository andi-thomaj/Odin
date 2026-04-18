using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class QpadmEra : BaseEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<QpadmPopulation> Populations { get; set; } = [];
    }

    public class QpadmEraConfiguration : IEntityTypeConfiguration<QpadmEra>
    {
        public void Configure(EntityTypeBuilder<QpadmEra> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
            builder.Property(e => e.Description).IsRequired().HasMaxLength(500);

            builder.ToTable("qpadm_eras");
        }
    }
}
