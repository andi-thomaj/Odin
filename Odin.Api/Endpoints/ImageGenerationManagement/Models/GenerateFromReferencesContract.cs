using System.ComponentModel.DataAnnotations;

namespace Odin.Api.Endpoints.ImageGenerationManagement.Models;

/// <summary>
/// Request for <c>POST api/admin/images/generate-from-references</c> (image edits, gpt-image-2). References
/// previously uploaded <c>reference_images</c> by id; OpenAI receives their bytes as input images. An
/// optional mask reference marks the region to regenerate. The response is the shared
/// <see cref="ImageJobContract.Response"/>.
/// </summary>
public static class GenerateFromReferencesContract
{
    public class Request : IValidatableObject
    {
        public required string Prompt { get; set; }
        public required List<int> ReferenceImageIds { get; set; } = [];
        public int? MaskReferenceImageId { get; set; }
        public string? InputFidelity { get; set; }
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
            foreach (var result in ImageRequestValidation.ValidateEnum(
                         InputFidelity, ImageParameterVocabulary.InputFidelities, "InputFidelity"))
                yield return result;

            if (ReferenceImageIds is null || ReferenceImageIds.Count == 0)
                yield return new ValidationResult("At least one reference image id is required.", ["ReferenceImageIds"]);
            else if (ReferenceImageIds.Any(id => id <= 0))
                yield return new ValidationResult("Reference image ids must be positive.", ["ReferenceImageIds"]);
        }
    }
}
