using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class Region
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int EthnicityId { get; set; }
        public required Ethnicity Ethnicity { get; set; }
    }

    public class RegionConfiguration : IEntityTypeConfiguration<Region>
    {
        public void Configure(EntityTypeBuilder<Region> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
            builder.ToTable("regions");
        }
    }
}
