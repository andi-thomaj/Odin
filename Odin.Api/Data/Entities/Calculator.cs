using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities
{
    public class Calculator : BaseEntity
    {
        public int Id { get; set; }
        public string Label { get; set; } = string.Empty;
        public string Coordinates { get; set; } = string.Empty;
        public CalculatorType Type { get; set; }
        public bool IsAdmin { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public int AdmixToolsEraId { get; set; }
        public AdmixToolsEra AdmixToolsEra { get; set; } = null!;
    }

    public class CalculatorConfiguration : IEntityTypeConfiguration<Calculator>
    {
        public void Configure(EntityTypeBuilder<Calculator> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Label).IsRequired().HasMaxLength(500);
            builder.Property(e => e.Coordinates).IsRequired().HasColumnType("text");
            builder.Property(e => e.Type)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50);
            builder.Property(e => e.IsAdmin).IsRequired();

            builder.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(e => e.AdmixToolsEra)
                .WithMany(e => e.Calculators)
                .HasForeignKey(e => e.AdmixToolsEraId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(e => e.Label);
            builder.HasIndex(e => e.Type);
            builder.HasIndex(e => e.IsAdmin);
            builder.HasIndex(e => e.AdmixToolsEraId);
            builder.HasIndex(e => new { e.UserId, e.Type });

            builder.ToTable("calculators");
        }
    }
}
