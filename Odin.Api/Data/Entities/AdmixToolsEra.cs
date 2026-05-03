using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class AdmixToolsEra
    {
        public int Id { get; set; }
        public required string Name { get; set; }

        public List<Calculator> Calculators { get; set; } = [];
    }

    public class AdmixToolsEraConfiguration : IEntityTypeConfiguration<AdmixToolsEra>
    {
        public void Configure(EntityTypeBuilder<AdmixToolsEra> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Name).IsRequired().HasMaxLength(100);
            builder.HasIndex(e => e.Name).IsUnique();

            builder.ToTable("admix_tools_eras");

            builder.HasData(
                new AdmixToolsEra { Id = 1, Name = "Ancient" },
                new AdmixToolsEra { Id = 2, Name = "Modern" }
            );
        }
    }
}
