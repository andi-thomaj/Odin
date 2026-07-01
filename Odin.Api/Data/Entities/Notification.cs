using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Entities
{
    public class Notification : BaseEntity
    {
        public int Id { get; set; }
        public int RecipientUserId { get; set; }
        public User RecipientUser { get; set; } = null!;
        public NotificationType Type { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public string? ReferenceId { get; set; }
    }

    public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
    {
        public void Configure(EntityTypeBuilder<Notification> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
            builder.Property(e => e.Message).IsRequired().HasMaxLength(1000);
            builder.Property(e => e.Type).IsRequired().HasConversion<string>().HasMaxLength(50);
            builder.Property(e => e.IsRead).HasDefaultValue(false);
            builder.Property(e => e.ReferenceId).HasMaxLength(100);

            builder.HasOne(e => e.RecipientUser)
                .WithMany(u => u.Notifications)
                .HasForeignKey(e => e.RecipientUserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => new { e.RecipientUserId, e.IsRead });

            builder.ToTable("notifications");
        }
    }
}
