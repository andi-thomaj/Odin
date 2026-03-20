using System.ComponentModel.DataAnnotations;
using Odin.Api.Data.Enums;
using OrderServiceEnum = Odin.Api.Data.Enums.OrderService;

namespace Odin.Api.Endpoints.OrderManagement.Models
{
    public class CreateOrderContract
    {
        private static readonly string[] AllowedFileExtensions = [".txt", ".csv", ".zip"];
        private const int MaxFileSizeBytes = 50 * 1024 * 1024;
        private static readonly string[] AllowedProfilePictureExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        private const int MaxProfilePictureSizeBytes = 5 * 1024 * 1024;

        public class Request : IValidatableObject
        {
            public required string FirstName { get; set; }
            public string? MiddleName { get; set; }
            public required string LastName { get; set; }
            public required string Gender { get; set; }
            public string? G25Coordinates { get; set; }
            public OrderServiceEnum Service { get; set; } = OrderServiceEnum.QPADM;
            public List<int> RegionIds { get; set; } = [];
            public IFormFile? File { get; set; }
            public int? ExistingFileId { get; set; }
            public IFormFile? ProfilePicture { get; set; }

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

                if (string.IsNullOrWhiteSpace(Gender) || !Enum.TryParse<Data.Enums.Gender>(Gender, out _))
                    yield return new ValidationResult("Gender is required and must be 'Male' or 'Female'.", [nameof(Gender)]);

                if (G25Coordinates?.Length > 500)
                    yield return new ValidationResult("G25 Coordinates must not exceed 500 characters.", [nameof(G25Coordinates)]);

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

                if (ProfilePicture is not null && ProfilePicture.Length > 0)
                {
                    var picExtension = Path.GetExtension(ProfilePicture.FileName).ToLowerInvariant();
                    if (!AllowedProfilePictureExtensions.Contains(picExtension))
                        yield return new ValidationResult(
                            $"Invalid profile picture type. Allowed types: {string.Join(", ", AllowedProfilePictureExtensions)}",
                            [nameof(ProfilePicture)]);

                    if (ProfilePicture.Length > MaxProfilePictureSizeBytes)
                        yield return new ValidationResult(
                            $"Profile picture size exceeds the maximum allowed size of {MaxProfilePictureSizeBytes / (1024 * 1024)} MB.",
                            [nameof(ProfilePicture)]);
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
        private static readonly string[] AllowedProfilePictureExtensions = [".jpg", ".jpeg", ".png", ".webp"];
        private const int MaxProfilePictureSizeBytes = 5 * 1024 * 1024;

        public class Request : IValidatableObject
        {
            public required string FirstName { get; set; }
            public string? MiddleName { get; set; }
            public required string LastName { get; set; }
            public string? G25Coordinates { get; set; }
            public List<int> RegionIds { get; set; } = [];
            public IFormFile? ProfilePicture { get; set; }

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

                if (G25Coordinates?.Length > 500)
                    yield return new ValidationResult("G25 Coordinates must not exceed 500 characters.", [nameof(G25Coordinates)]);

                if (RegionIds.Count == 0)
                    yield return new ValidationResult("At least one region must be selected.", [nameof(RegionIds)]);

                if (ProfilePicture is not null && ProfilePicture.Length > 0)
                {
                    var picExtension = Path.GetExtension(ProfilePicture.FileName).ToLowerInvariant();
                    if (!AllowedProfilePictureExtensions.Contains(picExtension))
                        yield return new ValidationResult(
                            $"Invalid profile picture type. Allowed types: {string.Join(", ", AllowedProfilePictureExtensions)}",
                            [nameof(ProfilePicture)]);

                    if (ProfilePicture.Length > MaxProfilePictureSizeBytes)
                        yield return new ValidationResult(
                            $"Profile picture size exceeds the maximum allowed size of {MaxProfilePictureSizeBytes / (1024 * 1024)} MB.",
                            [nameof(ProfilePicture)]);
                }
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
            public string? G25Coordinates { get; set; }
            public string? Gender { get; set; }
            public bool HasProfilePicture { get; set; }
            public bool HasViewedResults { get; set; }
            public List<int> RegionIds { get; set; } = [];
            /// <summary>Distinct ethnicity IDs implied by selected regions (parent-ancestry context).</summary>
            public List<int> EthnicityIds { get; set; } = [];
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
            public string Description { get; set; } = string.Empty;
            public decimal Percentage { get; set; }
            public decimal StandardError { get; set; }
            public decimal ZScore { get; set; }
            public string? GeoJson { get; set; }
        }

        public class EraGroupResult
        {
            public int EraId { get; set; }
            public required string EraName { get; set; }
            public decimal PiValue { get; set; }
            public string RightSources { get; set; } = string.Empty;
            public string LeftSources { get; set; } = string.Empty;
            public List<PopulationResult> Populations { get; set; } = [];
        }

        public class Response
        {
            public string FirstName { get; set; } = string.Empty;
            public string MiddleName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string? PaternalHaplogroup { get; set; }
            public bool HasMergedRawData { get; set; }
            public bool HasProfilePicture { get; set; }
            public string? Gender { get; set; }
            public List<EraGroupResult> EraGroups { get; set; } = [];
        }
    }

}
