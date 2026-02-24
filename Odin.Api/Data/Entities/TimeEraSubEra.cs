using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class TimeEraSubEra
    {
        public int TimeEraId { get; set; }
        public required TimeEra TimeEra { get; set; }
        public int SubEraId { get; set; }
        public required SubEra SubEra { get; set; }
    }

    public class TimeEraSubEraConfiguration : IEntityTypeConfiguration<TimeEraSubEra>
    {
        public void Configure(EntityTypeBuilder<TimeEraSubEra> builder)
        {
            builder.HasKey(e => new { e.TimeEraId, e.SubEraId });
            builder.HasOne(e => e.TimeEra)
                .WithMany(e => e.TimeEraSubEras)
                .HasForeignKey(e => e.TimeEraId);
            builder.HasOne(e => e.SubEra)
                .WithMany(e => e.TimeEraSubEras)
                .HasForeignKey(e => e.SubEraId);

            builder.ToTable("time_era_sub_eras");
        }
    }
}
