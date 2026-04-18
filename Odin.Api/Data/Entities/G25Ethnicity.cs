using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class G25Ethnicity : BaseEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int G25ContinentId { get; set; }
        public G25Continent G25Continent { get; set; } = null!;
        public List<G25Region> G25Regions { get; set; } = [];
    }

    public class G25EthnicityConfiguration : IEntityTypeConfiguration<G25Ethnicity>
    {
        public void Configure(EntityTypeBuilder<G25Ethnicity> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
            builder.HasIndex(e => e.Name).IsUnique();

            builder.HasOne(e => e.G25Continent)
                .WithMany(c => c.G25Ethnicities)
                .HasForeignKey(e => e.G25ContinentId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.ToTable("g25_ethnicities");
        }
    }
}
