using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities
{
    public static class AdmixtureSavedFileKind
    {
        public const string Source = "source";
        public const string Target = "target";

        public static bool IsValid(string? value) =>
            value == Source || value == Target;
    }

    public class AdmixtureSavedFile : BaseEntity
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public User User { get; set; } = null!;
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Kind { get; set; } = AdmixtureSavedFileKind.Source;
    }

    public class AdmixtureSavedFileConfiguration : IEntityTypeConfiguration<AdmixtureSavedFile>
    {
        public void Configure(EntityTypeBuilder<AdmixtureSavedFile> builder)
        {
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
            builder.Property(e => e.Content).IsRequired().HasColumnType("text");
            builder.Property(e => e.Kind)
                .IsRequired()
                .HasMaxLength(20)
                .HasDefaultValue(AdmixtureSavedFileKind.Source);

            builder.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(e => new { e.UserId, e.Kind, e.UpdatedAt });

            builder.ToTable("admixture_saved_files");
        }
    }
}
