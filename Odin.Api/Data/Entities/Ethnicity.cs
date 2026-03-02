using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class Ethnicity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public List<Region> Regions { get; set; } = [];
    }

    public class EthnicityConfiguration : IEntityTypeConfiguration<Ethnicity>
    {
        public void Configure(EntityTypeBuilder<Ethnicity> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);

            builder.ToTable("ethnicities");
        }
    }
}
