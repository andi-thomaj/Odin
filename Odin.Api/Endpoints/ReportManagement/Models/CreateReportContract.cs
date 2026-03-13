using System.ComponentModel.DataAnnotations;
using Odin.Api.Data.Enums;

namespace Odin.Api.Endpoints.ReportManagement.Models
{
    public class CreateReportContract
    {
        public class Request : IValidatableObject
        {
            public required string Type { get; set; }
            public required string Subject { get; set; }
            public required string Description { get; set; }
            public string? PageUrl { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (!Enum.TryParse<ReportType>(Type, ignoreCase: true, out _))
                {
                    yield return new ValidationResult(
                        "Type must be one of: Bug, FeatureRequest, InformationRequest.",
                        [nameof(Type)]);
                }

                if (string.IsNullOrWhiteSpace(Subject))
                {
                    yield return new ValidationResult("Subject is required.", [nameof(Subject)]);
                }
                else if (Subject.Length > 200)
                {
                    yield return new ValidationResult("Subject must not exceed 200 characters.",
                        [nameof(Subject)]);
                }

                if (string.IsNullOrWhiteSpace(Description))
                {
                    yield return new ValidationResult("Description is required.", [nameof(Description)]);
                }
                else if (Description.Length > 2000)
                {
                    yield return new ValidationResult("Description must not exceed 2000 characters.",
                        [nameof(Description)]);
                }

                if (PageUrl?.Length > 500)
                {
                    yield return new ValidationResult("Page URL must not exceed 500 characters.",
                        [nameof(PageUrl)]);
                }
            }
        }

        public class Response
        {
            public int Id { get; set; }
            public string Type { get; set; } = string.Empty;
            public string Subject { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public string? PageUrl { get; set; }
            public string? FileName { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
