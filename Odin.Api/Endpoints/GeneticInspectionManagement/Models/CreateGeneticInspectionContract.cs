using System.ComponentModel.DataAnnotations;

namespace Odin.Api.Endpoints.GeneticInspectionManagement.Models
{
    public class CreateGeneticInspectionContract
    {
        public class Request : IValidatableObject
        {
            public required string FirstName { get; set; }
            public string? MiddleName { get; set; }
            public required string LastName { get; set; }
            public required int RawGeneticFileId { get; set; }
            public List<int> RegionIds { get; set; } = [];

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (string.IsNullOrWhiteSpace(FirstName))
                {
                    yield return new ValidationResult("First name is required.", [nameof(FirstName)]);
                }

                if (FirstName?.Length < 2)
                {
                    yield return new ValidationResult("First name must be at least 2 characters.", [nameof(FirstName)]);
                }

                if (FirstName?.Length > 100)
                {
                    yield return new ValidationResult("First name must not exceed 100 characters.", [nameof(FirstName)]);
                }

                if (string.IsNullOrWhiteSpace(LastName))
                {
                    yield return new ValidationResult("Last name is required.", [nameof(LastName)]);
                }

                if (LastName?.Length < 2)
                {
                    yield return new ValidationResult("Last name must be at least 2 characters.", [nameof(LastName)]);
                }

                if (LastName?.Length > 100)
                {
                    yield return new ValidationResult("Last name must not exceed 100 characters.", [nameof(LastName)]);
                }

                if (MiddleName?.Length > 100)
                {
                    yield return new ValidationResult("Middle name must not exceed 100 characters.", [nameof(MiddleName)]);
                }

                if (RawGeneticFileId <= 0)
                {
                    yield return new ValidationResult("Raw genetic file ID is required.", [nameof(RawGeneticFileId)]);
                }

                if (RegionIds.Count == 0)
                {
                    yield return new ValidationResult("At least one region must be selected.", [nameof(RegionIds)]);
                }
            }
        }

        public class Response
        {
            public int Id { get; set; }
            public required string FirstName { get; set; }
            public string? MiddleName { get; set; }
            public required string LastName { get; set; }
            public int RawGeneticFileId { get; set; }
            public List<RegionResponse> Regions { get; set; } = [];
        }
    }

    public class GetGeneticInspectionContract
    {
        public class Response
        {
            public int Id { get; set; }
            public required string FirstName { get; set; }
            public string? MiddleName { get; set; }
            public required string LastName { get; set; }
            public int RawGeneticFileId { get; set; }
            public required string RawGeneticFileName { get; set; }
            public List<RegionResponse> Regions { get; set; } = [];
        }
    }

    public class RegionResponse
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string CountryName { get; set; }
    }
}
