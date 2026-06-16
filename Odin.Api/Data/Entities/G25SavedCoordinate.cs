using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class G25SavedCoordinate : BaseEntity, IAppScoped
    {
        public int Id { get; set; }
        /// <summary>Owning application (applications.key). Auto-stamped + query-filtered — see <see cref="IAppScoped"/>.</summary>
        public string App { get; set; } = string.Empty;
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public string RawInput { get; set; } = string.Empty;
        public bool Scaling { get; set; }
        public string AddMode { get; set; } = "aggregated";
        public string? CustomName { get; set; }
        public string ViewId { get; set; } = string.Empty;
    }

    public class G25SavedCoordinateConfiguration : IEntityTypeConfiguration<G25SavedCoordinate>
    {
        public void Configure(EntityTypeBuilder<G25SavedCoordinate> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
            builder.Property(e => e.RawInput).IsRequired().HasColumnType("text");
            builder.Property(e => e.AddMode).IsRequired().HasMaxLength(32);
            builder.Property(e => e.CustomName).HasMaxLength(200);
            builder.Property(e => e.ViewId).IsRequired().HasMaxLength(64);

            builder.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => new { e.UserId, e.UpdatedAt });

            builder.Property(e => e.App).IsRequired().HasMaxLength(50);

            builder.ToTable("g25_saved_coordinates");
        }
    }
}
