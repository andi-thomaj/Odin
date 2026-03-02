using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class Population : BaseEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int EraId { get; set; }
        public Era Era { get; set; }
        public List<SubPopulation> SubPopulations { get; set; } = [];
    }

    public class PopulationConfiguration : IEntityTypeConfiguration<Population>
    {
        public void Configure(EntityTypeBuilder<Population> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);

            builder.HasOne(e => e.Era)
                .WithMany(e => e.Populations)
                .HasForeignKey(e => e.EraId);

            builder.ToTable("populations");
        }
    }
}
