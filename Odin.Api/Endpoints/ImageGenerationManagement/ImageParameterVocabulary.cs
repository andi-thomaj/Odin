using System.Globalization;

namespace Odin.Api.Endpoints.ImageGenerationManagement;

/// <summary>
/// The gpt-image-2 parameter vocabulary used to validate requests and the settings resource. Quality,
/// background, format and moderation are small fixed enums. <b>Size is not a fixed list</b>: gpt-image-2
/// accepts <c>auto</c> or arbitrary custom dimensions (so callers can freely select dimensions) subject to
/// the constraints in <see cref="ValidateSize"/>. <see cref="ImageGenerationLimitsOptions"/> may further
/// cap total pixels at runtime, but never widen the model's vocabulary.
/// </summary>
public static class ImageParameterVocabulary
{
    /// <summary>Common preset sizes (hints for clients/UI). Any constraint-valid custom size is also accepted.</summary>
    public static readonly string[] SizePresets =
        ["auto", "1024x1024", "1536x1024", "1024x1536", "2048x2048", "2048x1152", "1152x2048"];

    public static readonly string[] Qualities = ["auto", "low", "medium", "high"];
    public static readonly string[] OutputFormats = ["png", "jpeg", "webp"];
    public static readonly string[] Backgrounds = ["auto", "transparent", "opaque"];
    public static readonly string[] ModerationLevels = ["auto", "low"];
    public static readonly string[] InputFidelities = ["high", "low"];

    // gpt-image-2 custom-dimension constraints.
    public const int SizeEdgeMultiple = 16;
    public const int MaxSizeEdge = 3840;
    public const long MinSizePixels = 655_360;
    public const long MaxSizePixels = 8_294_400;
    public const double MaxSizeAspectRatio = 3.0;

    public static bool IsValid(string[] allowed, string value) =>
        allowed.Contains(value, StringComparer.OrdinalIgnoreCase);

    /// <summary>Parse a <c>WIDTHxHEIGHT</c> size string into its dimensions.</summary>
    public static bool TryParseDimensions(string size, out int width, out int height)
    {
        width = 0;
        height = 0;
        var parts = size.Split('x', 2);
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out width)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out height);
    }

    /// <summary>
    /// Validates a size against gpt-image-2's rules: <c>auto</c>, or <c>WIDTHxHEIGHT</c> where both edges are
    /// multiples of 16, each ≤ 3840px, the long:short aspect ratio is ≤ 3:1, and the total pixel count is in
    /// [655,360, 8,294,400]. Returns an error message, or <c>null</c> when valid (or null/omitted).
    /// </summary>
    public static string? ValidateSize(string? size)
    {
        if (string.IsNullOrWhiteSpace(size) || string.Equals(size, "auto", StringComparison.OrdinalIgnoreCase))
            return null;

        if (!TryParseDimensions(size, out var width, out var height))
            return "Size must be 'auto' or 'WIDTHxHEIGHT' (e.g. 1024x1536).";

        if (width <= 0 || height <= 0)
            return "Size dimensions must be positive.";

        if (width % SizeEdgeMultiple != 0 || height % SizeEdgeMultiple != 0)
            return $"Size dimensions must be multiples of {SizeEdgeMultiple}.";

        if (width > MaxSizeEdge || height > MaxSizeEdge)
            return $"Size edges must be at most {MaxSizeEdge}px.";

        var ratio = (double)Math.Max(width, height) / Math.Min(width, height);
        if (ratio > MaxSizeAspectRatio)
            return $"Size aspect ratio must be at most {MaxSizeAspectRatio:0}:1.";

        var totalPixels = (long)width * height;
        if (totalPixels < MinSizePixels || totalPixels > MaxSizePixels)
            return $"Size total pixels must be between {MinSizePixels:N0} and {MaxSizePixels:N0}.";

        return null;
    }
}
