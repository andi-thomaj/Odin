using System.ComponentModel.DataAnnotations;
using Odin.Api.Data.Enums;
using OrderServiceEnum = Odin.Api.Data.Enums.OrderService;

namespace Odin.Api.Endpoints.OrderManagement.Models
{
    public class CreateOrderContract
    {
        private static readonly string[] AllowedFileExtensions = [".txt", ".csv", ".zip"];
        private const int MaxFileSizeBytes = 50 * 1024 * 1024;

        public class Request : IValidatableObject
        {
            public required string FirstName { get; set; }
            public string? MiddleName { get; set; }
            public required string LastName { get; set; }
            public OrderServiceEnum Service { get; set; } = OrderServiceEnum.QPADM;
            public List<int> RegionIds { get; set; } = [];
            public IFormFile? File { get; set; }
            public int? ExistingFileId { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (string.IsNullOrWhiteSpace(FirstName))
                    yield return new ValidationResult("First name is required.", [nameof(FirstName)]);
                else if (FirstName.Length < 2)
                    yield return new ValidationResult("First name must be at least 2 characters.", [nameof(FirstName)]);
                else if (FirstName.Length > 100)
                    yield return new ValidationResult("First name must not exceed 100 characters.", [nameof(FirstName)]);

                if (string.IsNullOrWhiteSpace(LastName))
                    yield return new ValidationResult("Last name is required.", [nameof(LastName)]);
                else if (LastName.Length < 2)
                    yield return new ValidationResult("Last name must be at least 2 characters.", [nameof(LastName)]);
                else if (LastName.Length > 100)
                    yield return new ValidationResult("Last name must not exceed 100 characters.", [nameof(LastName)]);

                if (MiddleName?.Length > 100)
                    yield return new ValidationResult("Middle name must not exceed 100 characters.", [nameof(MiddleName)]);

                if (!Enum.IsDefined(Service))
                    yield return new ValidationResult("Invalid service type.", [nameof(Service)]);

                if (RegionIds.Count == 0)
                    yield return new ValidationResult("At least one region must be selected.", [nameof(RegionIds)]);

                var hasFile = File is not null && File.Length > 0;
                var hasExistingId = ExistingFileId.HasValue && ExistingFileId.Value > 0;

                if (!hasFile && !hasExistingId)
                {
                    yield return new ValidationResult(
                        "A genetic file is required — upload a new file or select an existing one.",
                        [nameof(File), nameof(ExistingFileId)]);
                }

                if (hasFile)
                {
                    var extension = Path.GetExtension(File!.FileName).ToLowerInvariant();
                    if (!AllowedFileExtensions.Contains(extension))
                        yield return new ValidationResult(
                            $"Invalid file type. Allowed types: {string.Join(", ", AllowedFileExtensions)}",
                            [nameof(File)]);

                    if (File.Length > MaxFileSizeBytes)
                        yield return new ValidationResult(
                            $"File size exceeds the maximum allowed size of {MaxFileSizeBytes / (1024 * 1024)} MB.",
                            [nameof(File)]);
                }
            }
        }

        public class Response
        {
            public int Id { get; set; }
            public decimal Price { get; set; }
            public string Service { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public int GeneticInspectionId { get; set; }
        }
    }

    public class UpdateOrderContract
    {
        public class Request : IValidatableObject
        {
            public required string FirstName { get; set; }
            public string? MiddleName { get; set; }
            public required string LastName { get; set; }
            public List<int> RegionIds { get; set; } = [];

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (string.IsNullOrWhiteSpace(FirstName))
                    yield return new ValidationResult("First name is required.", [nameof(FirstName)]);
                else if (FirstName.Length < 2)
                    yield return new ValidationResult("First name must be at least 2 characters.", [nameof(FirstName)]);
                else if (FirstName.Length > 100)
                    yield return new ValidationResult("First name must not exceed 100 characters.", [nameof(FirstName)]);

                if (string.IsNullOrWhiteSpace(LastName))
                    yield return new ValidationResult("Last name is required.", [nameof(LastName)]);
                else if (LastName.Length < 2)
                    yield return new ValidationResult("Last name must be at least 2 characters.", [nameof(LastName)]);
                else if (LastName.Length > 100)
                    yield return new ValidationResult("Last name must not exceed 100 characters.", [nameof(LastName)]);

                if (MiddleName?.Length > 100)
                    yield return new ValidationResult("Middle name must not exceed 100 characters.", [nameof(MiddleName)]);

                if (RegionIds.Count == 0)
                    yield return new ValidationResult("At least one region must be selected.", [nameof(RegionIds)]);
            }
        }
    }

    public class GetOrderContract
    {
        public class Response
        {
            public int Id { get; set; }
            public decimal Price { get; set; }
            public string Service { get; set; } = string.Empty;
            public string Status { get; set; } = string.Empty;
            public int GeneticInspectionId { get; set; }
            public string FirstName { get; set; } = string.Empty;
            public string MiddleName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public List<int> RegionIds { get; set; } = [];
            public DateTime CreatedAt { get; set; }
            public string CreatedBy { get; set; } = string.Empty;
            public DateTime UpdatedAt { get; set; }
            public string UpdatedBy { get; set; } = string.Empty;
        }
    }

    public class GetOrderQpadmResultContract
    {
        public class PopulationResult
        {
            public int Id { get; set; }
            public required string Name { get; set; }
            public int EraId { get; set; }
            public required string EraName { get; set; }
            public decimal Percentage { get; set; }
            public decimal StandardError { get; set; }
            public decimal ZScore { get; set; }
        }

        public class Response
        {
            public string FirstName { get; set; } = string.Empty;
            public string MiddleName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public decimal PiValue { get; set; }
            public string RightSources { get; set; } = string.Empty;
            public string LeftSources { get; set; } = string.Empty;
            public List<PopulationResult> Populations { get; set; } = [];
        }
    }

}
