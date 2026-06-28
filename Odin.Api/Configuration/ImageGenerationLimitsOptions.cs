namespace Odin.Api.Configuration;

/// <summary>
/// Server-side guardrails for the admin image-generation endpoints. These bound cost and abuse without
/// a redeploy (tunable via appsettings / Coolify env), and back the request validation in
/// <c>GenerateImageContract</c> / <c>GenerateFromReferencesContract</c>. Bound from the
/// <c>ImageGenerationLimits</c> section.
/// </summary>
public sealed class ImageGenerationLimitsOptions
{
    public const string SectionName = "ImageGenerationLimits";

    /// <summary>Max images a single generate/edit request may ask for (<c>n</c>). gpt-image-2 allows up to 10; capped lower to bound cost.</summary>
    public int MaxImagesPerRequest { get; set; } = 4;

    /// <summary>Max reference images a single edit request may reference. gpt-image-2 accepts up to 16.</summary>
    public int MaxReferenceImagesPerRequest { get; set; } = 16;

    /// <summary>Max byte size of a single uploaded reference image (default 25 MB; OpenAI's per-image limit is 50 MB).</summary>
    public long MaxReferenceUploadBytes { get; set; } = 25L * 1024 * 1024;

    /// <summary>
    /// Cost cap on the total pixels of a requested custom dimension. Defaults to gpt-image-2's hard maximum
    /// (8,294,400 ≈ 3840×2160), so any model-valid size is allowed; lower it to bound spend (e.g. 4,194,304
    /// for a 2048×2048 ceiling). Does not affect <c>auto</c>.
    /// </summary>
    public long MaxImagePixels { get; set; } = 8_294_400;

    /// <summary>Allowed <c>quality</c> values for gpt-image-2.</summary>
    public string[] AllowedQualities { get; set; } = ["auto", "low", "medium", "high"];

    /// <summary>Allowed <c>output_format</c> values.</summary>
    public string[] AllowedOutputFormats { get; set; } = ["png", "jpeg", "webp"];

    /// <summary>Allowed <c>background</c> values (<c>transparent</c> requires png/webp).</summary>
    public string[] AllowedBackgrounds { get; set; } = ["auto", "transparent", "opaque"];

    /// <summary>Allowed <c>moderation</c> values for gpt-image-2.</summary>
    public string[] AllowedModerationLevels { get; set; } = ["auto", "low"];
}
