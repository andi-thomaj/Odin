using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

/// <summary>
/// Single-row (<c>Id = 1</c>) admin-editable defaults for image generation. When a generate request omits
/// a parameter, the service falls back to the value here. Exposed via an <c>AdminOnly</c> GET/PUT and
/// memory-cached (invalidated on write). A dedicated entity rather than the bool-only
/// <c>AppSettings</c> store so the typed fields surface cleanly in Swagger / the generated clients.
/// </summary>
public class ImageGenerationSettings : BaseEntity
{
    /// <summary>Fixed singleton key. Always 1.</summary>
    public int Id { get; set; }

    public string Model { get; set; } = "gpt-image-2";
    public string Size { get; set; } = "1024x1024";
    public string Quality { get; set; } = "medium";
    public string Background { get; set; } = "auto";
    public string OutputFormat { get; set; } = "png";
    public int? OutputCompression { get; set; }
    public string Moderation { get; set; } = "auto";
    public int DefaultN { get; set; } = 1;
}

public class ImageGenerationSettingsConfiguration : IEntityTypeConfiguration<ImageGenerationSettings>
{
    public void Configure(EntityTypeBuilder<ImageGenerationSettings> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Model).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Size).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Quality).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Background).IsRequired().HasMaxLength(20);
        builder.Property(e => e.OutputFormat).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Moderation).IsRequired().HasMaxLength(20);

        builder.ToTable("image_generation_settings");
    }
}
