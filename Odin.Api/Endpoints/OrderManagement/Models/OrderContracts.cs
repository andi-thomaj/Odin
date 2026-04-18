using System.ComponentModel.DataAnnotations;
using Odin.Api.Data.Enums;

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
            public ServiceType Service { get; set; } = ServiceType.qpAdm;
            public List<int> RegionIds { get; set; } = [];
            public List<int> AddonIds { get; set; } = [];
            public string? PromoCode { get; set; }
            public IFormFile? File { get; set; }
            public int? ExistingFileId { get; set; }
            public string? G25Coordinates { get; set; }
            public IFormFile? ProfilePicture { get; set; }
            public int? PaddlePaymentId { get; set; }

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

                if (!Enum.IsDefined(Service))
                    yield return new ValidationResult("Invalid service type.", [nameof(Service)]);

                if (Service == ServiceType.qpAdm && RegionIds.Count == 0)
                    yield return new ValidationResult("At least one region must be selected.", [nameof(RegionIds)]);

                var hasFile = File is not null && File.Length > 0;
                var hasExistingId = ExistingFileId.HasValue && ExistingFileId.Value > 0;
                var hasCoordinates = !string.IsNullOrWhiteSpace(G25Coordinates);

                if (Service == ServiceType.g25)
                {
                    if (!hasFile && !hasExistingId && !hasCoordinates)
                    {
                        yield return new ValidationResult(
                            "Provide G25 coordinates, or upload a genetic file, or select an existing one.",
                            [nameof(File), nameof(ExistingFileId), nameof(G25Coordinates)]);
                    }

                    if (hasCoordinates && G25Coordinates!.Length > 500)
                    {
                        yield return new ValidationResult(
                            "G25 coordinates must not exceed 500 characters.",
                            [nameof(G25Coordinates)]);
                    }
                }
                else if (!hasFile && !hasExistingId)
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

    public class GetOrderG25ResultContract
    {
        public class DistancePopulationResult
        {
            public required string Name { get; set; }
            public double Distance { get; set; }
            public int Rank { get; set; }
        }

        public class DistanceEraResult
        {
            public int EraId { get; set; }
            public required string EraName { get; set; }
            public List<DistancePopulationResult> Populations { get; set; } = [];
        }

        public class AdmixtureAncestorResult
        {
            public required string Name { get; set; }
            public double Percentage { get; set; }
        }

        public class AdmixtureResult
        {
            public double FitDistance { get; set; }
            public List<AdmixtureAncestorResult> Ancestors { get; set; } = [];
        }

        public class PcaFileResult
        {
            public int Id { get; set; }
            public required string FileName { get; set; }
        }

        public class PcaContinentResult
        {
            public int ContinentId { get; set; }
            public required string ContinentName { get; set; }
            public List<PcaFileResult> Files { get; set; } = [];
        }

        public class Response
        {
            public string FirstName { get; set; } = string.Empty;
            public string MiddleName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string? Gender { get; set; }
            public bool HasProfilePicture { get; set; }
            public string? G25Coordinates { get; set; }
            public List<DistanceEraResult> DistanceEras { get; set; } = [];
            public AdmixtureResult? Admixture { get; set; }
            public List<PcaContinentResult> Pca { get; set; } = [];
        }
    }

    public class GetOrderQpadmResultContract
    {
        public class PopulationResult
        {
            public int Id { get; set; }
            public required string Name { get; set; }
            public string Description { get; set; } = string.Empty;
            public string GeoJson { get; set; } = string.Empty;
            public string IconFileName { get; set; } = string.Empty;
            public string Color { get; set; } = string.Empty;
            public int MusicTrackId { get; set; }
            public string MusicTrackFileName { get; set; } = string.Empty;
            public bool HasAudioFile { get; set; }
            public decimal Percentage { get; set; }
            public decimal StandardError { get; set; }
            public decimal ZScore { get; set; }
        }

        public class EraGroupResult
        {
            public int EraId { get; set; }
            public required string EraName { get; set; }
            public decimal PValue { get; set; }
            public string RightSources { get; set; } = string.Empty;
            public List<PopulationResult> Populations { get; set; } = [];
        }

        public class Response
        {
            public string FirstName { get; set; } = string.Empty;
            public string MiddleName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public bool HasMergedRawData { get; set; }
            public bool HasProfilePicture { get; set; }
            public string? Gender { get; set; }
            public int? IntroTrackId { get; set; }
            public bool HasIntroAudioFile { get; set; }
            public List<EraGroupResult> EraGroups { get; set; } = [];
        }
    }

}
