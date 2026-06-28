using System.ComponentModel.DataAnnotations;

namespace Odin.Api.Endpoints.ImageGenerationManagement.Models;

/// <summary>
/// Shared <see cref="IValidatableObject"/> checks for the generate/edit/settings contracts. Validates the
/// fixed gpt-image-2 vocabulary and basic ranges; the configurable per-request caps
/// (<c>ImageGenerationLimitsOptions.MaxImagesPerRequest</c>, reference-image counts) are enforced in the
/// service where the options are available.
/// </summary>
internal static class ImageRequestValidation
{
    public static IEnumerable<ValidationResult> ValidatePrompt(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            yield return new ValidationResult("Prompt is required.", ["Prompt"]);
        else if (prompt.Length > GenerateImageContract.MaxPromptLength)
            yield return new ValidationResult(
                $"Prompt must not exceed {GenerateImageContract.MaxPromptLength} characters.", ["Prompt"]);
    }

    public static IEnumerable<ValidationResult> ValidateParameters(
        int? n, string? size, string? quality, string? background,
        string? outputFormat, int? outputCompression, string? moderation)
    {
        if (n is { } count && count is < 1 or > 10)
            yield return new ValidationResult("N must be between 1 and 10.", ["N"]);

        if (ImageParameterVocabulary.ValidateSize(size) is { } sizeError)
            yield return new ValidationResult(sizeError, ["Size"]);
        foreach (var result in ValidateEnum(quality, ImageParameterVocabulary.Qualities, "Quality"))
            yield return result;
        foreach (var result in ValidateEnum(background, ImageParameterVocabulary.Backgrounds, "Background"))
            yield return result;
        foreach (var result in ValidateEnum(outputFormat, ImageParameterVocabulary.OutputFormats, "OutputFormat"))
            yield return result;
        foreach (var result in ValidateEnum(moderation, ImageParameterVocabulary.ModerationLevels, "Moderation"))
            yield return result;

        if (outputCompression is { } compression && compression is < 0 or > 100)
            yield return new ValidationResult("OutputCompression must be between 0 and 100.", ["OutputCompression"]);
    }

    public static IEnumerable<ValidationResult> ValidateEnum(string? value, string[] allowed, string member)
    {
        if (value is not null && !ImageParameterVocabulary.IsValid(allowed, value))
            yield return new ValidationResult($"{member} must be one of: {string.Join(", ", allowed)}.", [member]);
    }
}
