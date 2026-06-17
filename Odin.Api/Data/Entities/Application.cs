using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    /// <summary>
    /// Registry of the applications that share this backend + database (e.g. odin-react = "ancestrify",
    /// odin-aurora = "aurora"). <see cref="Key"/> is what each frontend sends in the <c>X-App</c> header and
    /// what every <see cref="IAppScoped"/> row stores. Adding a new app = inserting one row here and setting
    /// the header in the new frontend — no schema change. Also carries per-app branding (frontend URL,
    /// from-email) so emails/redirects can be app-specific. This is shared reference data: it is NOT
    /// <see cref="IAppScoped"/> and is cached like other reference tables.
    /// </summary>
    public class Application
    {
        public string Key { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string FrontendBaseUrl { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string? FromName { get; set; }
        public bool IsActive { get; set; } = true;
    }

    public class ApplicationConfiguration : IEntityTypeConfiguration<Application>
    {
        public void Configure(EntityTypeBuilder<Application> builder)
        {
            builder.HasKey(e => e.Key);
            builder.Property(e => e.Key).HasMaxLength(50);
            builder.Property(e => e.DisplayName).IsRequired().HasMaxLength(100);
            builder.Property(e => e.FrontendBaseUrl).IsRequired().HasMaxLength(300);
            builder.Property(e => e.FromEmail).IsRequired().HasMaxLength(200);
            builder.Property(e => e.FromName).HasMaxLength(100);
            builder.Property(e => e.IsActive).HasDefaultValue(true);

            builder.ToTable("applications");
        }
    }
}
