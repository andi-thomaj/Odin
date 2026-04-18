using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class G25Era : BaseEntity
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public G25DistanceFile? DistanceFile { get; set; }
        public G25PcaFile? PcaFile { get; set; }
    }

    public class G25EraConfiguration : IEntityTypeConfiguration<G25Era>
    {
        public void Configure(EntityTypeBuilder<G25Era> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
            builder.HasIndex(e => e.Name).IsUnique();

            builder.ToTable("g25_eras");
        }
    }
}
