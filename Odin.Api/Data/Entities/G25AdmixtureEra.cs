using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class G25AdmixtureEra : BaseEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public List<G25PopulationSample> G25PopulationSamples { get; set; } = [];
    }

    public class G25AdmixtureEraConfiguration : IEntityTypeConfiguration<G25AdmixtureEra>
    {
        public void Configure(EntityTypeBuilder<G25AdmixtureEra> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
            builder.HasIndex(e => e.Name).IsUnique();

            builder.ToTable("g25_admixture_eras");
        }
    }
}
