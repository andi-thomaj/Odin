using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class G25DistanceEra : BaseEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public G25DistanceFile? DistanceFile { get; set; }
        public G25PcaFile? PcaFile { get; set; }
    }

    public class G25DistanceEraConfiguration : IEntityTypeConfiguration<G25DistanceEra>
    {
        public void Configure(EntityTypeBuilder<G25DistanceEra> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
            builder.HasIndex(e => e.Name).IsUnique();

            builder.ToTable("g25_distance_eras");
        }
    }
}
