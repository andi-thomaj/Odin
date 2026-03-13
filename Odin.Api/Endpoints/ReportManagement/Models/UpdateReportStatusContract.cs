using System.ComponentModel.DataAnnotations;
using Odin.Api.Data.Enums;

namespace Odin.Api.Endpoints.ReportManagement.Models
{
    public class UpdateReportStatusContract
    {
        public class Request : IValidatableObject
        {
            public required string Status { get; set; }
            public string? AdminNotes { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (!Enum.TryParse<ReportStatus>(Status, ignoreCase: true, out _))
                {
                    yield return new ValidationResult(
                        "Status must be one of: Pending, InReview, Resolved, Closed.",
                        [nameof(Status)]);
                }

                if (AdminNotes?.Length > 1000)
                {
                    yield return new ValidationResult("Admin notes must not exceed 1000 characters.",
                        [nameof(AdminNotes)]);
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
            public string? AdminNotes { get; set; }
            public string? PageUrl { get; set; }
            public string? FileName { get; set; }
            public string UserName { get; set; } = string.Empty;
            public string UserEmail { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }
    }
}
