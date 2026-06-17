using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class User : BaseEntity, IAppScoped
    {
        public int Id { get; set; }
        /// <summary>Owning application (applications.key). Auto-stamped + query-filtered — see <see cref="IAppScoped"/>.</summary>
        public string App { get; set; } = string.Empty;
        public string IdentityId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public AppRole Role { get; set; } = AppRole.User;
        public string? Country { get; set; }
        public string? CountryCode { get; set; }

        public List<QpadmGeneticInspection> GeneticInspections { get; set; } = [];
        public List<Notification> Notifications { get; set; } = [];
        public List<Report> Reports { get; set; } = [];
    }

    public class UserConfiguration : IEntityTypeConfiguration<User>
    {
        public void Configure(EntityTypeBuilder<User> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Username).IsRequired().HasMaxLength(100);
            builder.Property(e => e.Email).IsRequired().HasMaxLength(200);
            builder.Property(e => e.Role)
                .HasConversion<string>()
                .HasMaxLength(50)
                .HasDefaultValue(AppRole.User);
            builder.Property(e => e.Country).HasMaxLength(100);
            builder.Property(e => e.CountryCode).HasMaxLength(2);
            builder.Property(e => e.App).IsRequired().HasMaxLength(50);

            // Composite identity: the same Auth0 sub is a SEPARATE account per application.
            builder.HasIndex(e => new { e.IdentityId, e.App }).IsUnique();

            builder.ToTable("application_users");
        }
    }
}
