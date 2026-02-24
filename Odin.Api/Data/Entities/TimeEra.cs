using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class TimeEra : BaseEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<TimeEraSubEra> TimeEraSubEras { get; set; } = [];
    }

    public class TimeEraConfiguration : IEntityTypeConfiguration<TimeEra>
    {
        public void Configure(EntityTypeBuilder<TimeEra> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);

            builder.ToTable("time_eras");
        }
    }
}
