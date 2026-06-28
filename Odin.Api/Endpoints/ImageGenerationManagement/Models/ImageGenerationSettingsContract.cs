using System.ComponentModel.DataAnnotations;

namespace Odin.Api.Endpoints.ImageGenerationManagement.Models;

/// <summary>
/// The admin-editable default image-generation settings (<c>GET/PUT api/admin/images/settings</c>). These
/// values seed any parameter a generate request omits.
/// </summary>
public static class ImageGenerationSettingsContract
{
    public class Request : IValidatableObject
    {
        public required string Size { get; set; }
        public required string Quality { get; set; }
        public required string Background { get; set; }
        public required string OutputFormat { get; set; }
        public int? OutputCompression { get; set; }
        public required string Moderation { get; set; }
        public int DefaultN { get; set; } = 1;

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ImageParameterVocabulary.ValidateSize(Size) is { } sizeError)
                yield return new ValidationResult(sizeError, ["Size"]);
            foreach (var result in ImageRequestValidation.ValidateEnum(Quality, ImageParameterVocabulary.Qualities, "Quality"))
                yield return result;
            foreach (var result in ImageRequestValidation.ValidateEnum(Background, ImageParameterVocabulary.Backgrounds, "Background"))
                yield return result;
            foreach (var result in ImageRequestValidation.ValidateEnum(OutputFormat, ImageParameterVocabulary.OutputFormats, "OutputFormat"))
                yield return result;
            foreach (var result in ImageRequestValidation.ValidateEnum(Moderation, ImageParameterVocabulary.ModerationLevels, "Moderation"))
                yield return result;

            if (DefaultN is < 1 or > 10)
                yield return new ValidationResult("DefaultN must be between 1 and 10.", ["DefaultN"]);
            if (OutputCompression is { } compression && compression is < 0 or > 100)
                yield return new ValidationResult("OutputCompression must be between 0 and 100.", ["OutputCompression"]);
        }
    }

    public sealed class Response
    {
        public required string Model { get; set; }
        public required string Size { get; set; }
        public required string Quality { get; set; }
        public required string Background { get; set; }
        public required string OutputFormat { get; set; }
        public int? OutputCompression { get; set; }
        public required string Moderation { get; set; }
        public int DefaultN { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
