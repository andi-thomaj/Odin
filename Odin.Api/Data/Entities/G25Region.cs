using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class G25Region : BaseEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public List<G25Ethnicity> Ethnicities { get; set; } = [];
    }

    public class G25RegionConfiguration : IEntityTypeConfiguration<G25Region>
    {
        public void Configure(EntityTypeBuilder<G25Region> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
            builder.HasIndex(e => e.Name).IsUnique();

            builder.ToTable("g25_regions");
        }
    }
}
