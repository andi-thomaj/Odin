using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class G25Continent : BaseEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public List<G25Ethnicity> G25Ethnicities { get; set; } = [];
    }

    public class G25ContinentConfiguration : IEntityTypeConfiguration<G25Continent>
    {
        public void Configure(EntityTypeBuilder<G25Continent> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
            builder.HasIndex(e => e.Name).IsUnique();

            builder.ToTable("g25_continents");
        }
    }
}
