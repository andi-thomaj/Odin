using System.ComponentModel.DataAnnotations;

namespace Odin.Api.Endpoints.RawGeneticFileManagement.Models
{
    public class UploadGeneticFileContract
    {
        public class Request : IValidatableObject
        {
            public required IFormFile File { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                var allowedExtensions = new[] { ".txt", ".csv", ".zip" };
                var maxFileSize = 50 * 1024 * 1024; // 50 MB

                if (File is null || File.Length == 0)
                {
                    yield return new ValidationResult("File is required.", [nameof(File)]);
                    yield break;
                }

                var extension = Path.GetExtension(File.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    yield return new ValidationResult(
                        $"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}",
                        [nameof(File)]);
                }

                if (File.Length > maxFileSize)
                {
                    yield return new ValidationResult(
                        $"File size exceeds the maximum allowed size of {maxFileSize / (1024 * 1024)} MB.",
                        [nameof(File)]);
                }
            }
        }

        public class Response
        {
            public int Id { get; set; }
            public required string FileName { get; set; }
            public long FileSize { get; set; }
            public DateTime UploadedAt { get; set; }
        }
    }

    public class GetGeneticFileContract
    {
        public class Response
        {
            public int Id { get; set; }
            public required string FileName { get; set; }
            public long FileSize { get; set; }
        }
    }
}
