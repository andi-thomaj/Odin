using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class SubPopulation : BaseEntity
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public int PopulationId { get; set; }
        public Population Population { get; set; }
    }

    public class SubPopulationConfiguration : IEntityTypeConfiguration<SubPopulation>
    {
        public void Configure(EntityTypeBuilder<SubPopulation> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);

            builder.HasOne(e => e.Population)
                .WithMany(e => e.SubPopulations)
                .HasForeignKey(e => e.PopulationId);

            builder.ToTable("sub_populations");
        }
    }
}
