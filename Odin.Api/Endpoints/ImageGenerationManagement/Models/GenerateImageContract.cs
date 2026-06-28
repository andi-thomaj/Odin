using System.ComponentModel.DataAnnotations;

namespace Odin.Api.Endpoints.ImageGenerationManagement.Models;

/// <summary>
/// Request/response for <c>POST api/admin/images/generate</c> (text-to-image, gpt-image-2). Any omitted
/// parameter falls back to the persisted default settings. <see cref="Request.Async"/> opts into
/// background processing (202 + job id to poll) instead of waiting inline.
/// </summary>
public static class GenerateImageContract
{
    public const int MaxPromptLength = 32_000;

    public class Request : IValidatableObject
    {
        public required string Prompt { get; set; }
        public int? N { get; set; }
        public string? Size { get; set; }
        public string? Quality { get; set; }
        public string? Background { get; set; }
        public string? OutputFormat { get; set; }
        public int? OutputCompression { get; set; }
        public string? Moderation { get; set; }
        public bool Async { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            foreach (var result in ImageRequestValidation.ValidatePrompt(Prompt))
                yield return result;
            foreach (var result in ImageRequestValidation.ValidateParameters(
                         N, Size, Quality, Background, OutputFormat, OutputCompression, Moderation))
                yield return result;
        }
    }
}
