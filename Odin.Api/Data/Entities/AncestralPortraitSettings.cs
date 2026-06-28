using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Odin.Api.Data.Entities;

/// <summary>
/// Single-row (<c>Id = 1</c>) admin-editable settings for the paid "Through the Ages" ancestral-portrait generation —
/// fully runtime-configurable from the WEB admin (model, quality, size, variations, caps, cost rates) without a
/// redeploy. Mirrors <see cref="ImageGenerationSettings"/>: AdminOnly GET/PUT, memory-cached (invalidated on write),
/// seeded from the property defaults below on first read. The generation worker reads from here.
/// </summary>
public class AncestralPortraitSettings : BaseEntity
{
    /// <summary>Fixed singleton key. Always 1.</summary>
    public int Id { get; set; }

    public string Model { get; set; } = "gpt-image-2";
    public string Size { get; set; } = "1024x1536";
    public string Quality { get; set; } = "medium";
    public string Background { get; set; } = "auto";
    public string OutputFormat { get; set; } = "jpeg";
    public string Moderation { get; set; } = "auto";

    /// <summary>Portrait images generated per era. Default 1 — a single image of the era's top ancestral population
    /// (no variations to pick between). The admin can raise it, but the iOS "Ages" story shows one image per era.</summary>
    public int VariationsPerEra { get; set; } = 1;
    /// <summary>Max eras turned into portraits (one group per era).</summary>
    public int MaxEras { get; set; } = 6;
    /// <summary>Face reference photos passed to the model (≤16).</summary>
    public int MaxFaceReferences { get; set; } = 6;

    /// <summary>Cost-estimate rates (USD per 1,000,000 tokens) for the first-party cost tally. Tune to OpenAI's pricing.</summary>
    public decimal CostPerMillionInputTokensUsd { get; set; } = 10m;
    public decimal CostPerMillionOutputTokensUsd { get; set; } = 40m;
}

public class AncestralPortraitSettingsConfiguration : IEntityTypeConfiguration<AncestralPortraitSettings>
{
    public void Configure(EntityTypeBuilder<AncestralPortraitSettings> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Model).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Size).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Quality).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Background).IsRequired().HasMaxLength(20);
        builder.Property(e => e.OutputFormat).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Moderation).IsRequired().HasMaxLength(20);
        builder.Property(e => e.CostPerMillionInputTokensUsd).HasPrecision(10, 4);
        builder.Property(e => e.CostPerMillionOutputTokensUsd).HasPrecision(10, 4);

        builder.ToTable("ancestral_portrait_settings");
    }
}
