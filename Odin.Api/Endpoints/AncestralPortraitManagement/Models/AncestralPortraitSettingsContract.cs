using System.ComponentModel.DataAnnotations;
using Odin.Api.Endpoints.ImageGenerationManagement;
using Odin.Api.Endpoints.ImageGenerationManagement.Models;

namespace Odin.Api.Endpoints.AncestralPortraitManagement.Models;

/// <summary>
/// Admin-editable settings for the AI ancestral-portrait generation (<c>GET/PUT api/admin/ancestral-portraits/settings</c>,
/// AdminOnly, web-only). Fully runtime-configurable — model, quality, size, variations, caps, cost rates.
/// </summary>
public static class AncestralPortraitSettingsContract
{
    public class Request : IValidatableObject
    {
        public required string Model { get; set; }
        public required string Size { get; set; }
        public required string Quality { get; set; }
        public required string Background { get; set; }
        public required string OutputFormat { get; set; }
        public required string Moderation { get; set; }
        public int VariationsPerEra { get; set; } = 1;
        public int MaxEras { get; set; } = 6;
        public int MaxFaceReferences { get; set; } = 6;
        public decimal CostPerMillionInputTokensUsd { get; set; } = 10m;
        public decimal CostPerMillionOutputTokensUsd { get; set; } = 40m;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (string.IsNullOrWhiteSpace(Model))
                yield return new ValidationResult("Model is required.", ["Model"]);
            if (ImageParameterVocabulary.ValidateSize(Size) is { } sizeError)
                yield return new ValidationResult(sizeError, ["Size"]);
            foreach (var r in ImageRequestValidation.ValidateEnum(Quality, ImageParameterVocabulary.Qualities, "Quality"))
                yield return r;
            foreach (var r in ImageRequestValidation.ValidateEnum(Background, ImageParameterVocabulary.Backgrounds, "Background"))
                yield return r;
            foreach (var r in ImageRequestValidation.ValidateEnum(OutputFormat, ImageParameterVocabulary.OutputFormats, "OutputFormat"))
                yield return r;
            foreach (var r in ImageRequestValidation.ValidateEnum(Moderation, ImageParameterVocabulary.ModerationLevels, "Moderation"))
                yield return r;

            if (VariationsPerEra is < 1 or > 8)
                yield return new ValidationResult("VariationsPerEra must be between 1 and 8.", ["VariationsPerEra"]);
            if (MaxEras is < 1 or > 20)
                yield return new ValidationResult("MaxEras must be between 1 and 20.", ["MaxEras"]);
            if (MaxFaceReferences is < 1 or > 16)
                yield return new ValidationResult("MaxFaceReferences must be between 1 and 16.", ["MaxFaceReferences"]);
            if (CostPerMillionInputTokensUsd < 0 || CostPerMillionOutputTokensUsd < 0)
                yield return new ValidationResult("Cost rates must be non-negative.", ["CostPerMillionInputTokensUsd"]);
        }
    }

    public sealed class Response
    {
        public required string Model { get; set; }
        public required string Size { get; set; }
        public required string Quality { get; set; }
        public required string Background { get; set; }
        public required string OutputFormat { get; set; }
        public required string Moderation { get; set; }
        public int VariationsPerEra { get; set; }
        public int MaxEras { get; set; }
        public int MaxFaceReferences { get; set; }
        public decimal CostPerMillionInputTokensUsd { get; set; }
        public decimal CostPerMillionOutputTokensUsd { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
