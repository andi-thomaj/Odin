using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities
{
    public class Report : BaseEntity
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public ReportType Type { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public ReportStatus Status { get; set; } = ReportStatus.Pending;
        public string? AdminNotes { get; set; }
        public string? FileName { get; set; }
        public byte[]? FileData { get; set; }
        public string? FileContentType { get; set; }
        public string? PageUrl { get; set; }
    }

    public class ReportConfiguration : IEntityTypeConfiguration<Report>
    {
        public void Configure(EntityTypeBuilder<Report> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Type).IsRequired().HasConversion<string>().HasMaxLength(50);
            builder.Property(e => e.Subject).IsRequired().HasMaxLength(200);
            builder.Property(e => e.Description).IsRequired().HasMaxLength(2000);
            builder.Property(e => e.Status).IsRequired().HasConversion<string>().HasMaxLength(50).HasDefaultValue(ReportStatus.Pending);
            builder.Property(e => e.AdminNotes).HasMaxLength(1000);
            builder.Property(e => e.FileName).HasMaxLength(255);
            builder.Property(e => e.FileContentType).HasMaxLength(100);
            builder.Property(e => e.PageUrl).HasMaxLength(500);

            builder.HasOne(e => e.User)
                .WithMany(u => u.Reports)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => new { e.UserId, e.Status });

            builder.ToTable("reports");
        }
    }
}
