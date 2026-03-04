using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public class User : BaseEntity
    {
        public int Id { get; set; }
        public string IdentityId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string MiddleName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public AppRole Role { get; set; } = AppRole.User;
        public List<GeneticInspection> GeneticInspections { get; set; } = [];
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

            builder.ToTable("application_users");
        }
    }
}
