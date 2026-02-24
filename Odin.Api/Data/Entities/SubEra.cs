using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class SubEra : BaseEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<TimeEraSubEra> TimeEraSubEras { get; set; } = [];
    }

    public class SubEraConfiguration : IEntityTypeConfiguration<SubEra>
    {
        public void Configure(EntityTypeBuilder<SubEra> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);

            builder.ToTable("sub_eras");
        }
    }
}
