using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class G25TargetCoordinate : BaseEntity, IAppScoped
    {
        public int Id { get; set; }
        /// <summary>Owning application (applications.key). Auto-stamped + query-filtered — see <see cref="IAppScoped"/>.</summary>
        public string App { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Coordinates { get; set; } = string.Empty;
        public int UserId { get; set; }
        public User User { get; set; } = null!;
    }

    public class G25TargetCoordinateConfiguration : IEntityTypeConfiguration<G25TargetCoordinate>
    {
        public void Configure(EntityTypeBuilder<G25TargetCoordinate> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Label).IsRequired().HasMaxLength(500);
            builder.Property(e => e.Coordinates).IsRequired().HasColumnType("text");

            builder.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => new { e.UserId, e.UpdatedAt });

            builder.Property(e => e.App).IsRequired().HasMaxLength(50);

            builder.ToTable("g25_target_coordinates");
        }
    }
}
