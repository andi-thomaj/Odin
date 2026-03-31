using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class Era : BaseEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<Population> Populations { get; set; } = [];
    }

    public class EraConfiguration : IEntityTypeConfiguration<Era>
    {
        public void Configure(EntityTypeBuilder<Era> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
            builder.Property(e => e.Description).IsRequired().HasMaxLength(500);

            builder.ToTable("eras");
        }
    }
}
