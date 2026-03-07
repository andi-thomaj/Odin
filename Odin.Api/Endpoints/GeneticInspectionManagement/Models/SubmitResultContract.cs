using System.ComponentModel.DataAnnotations;

namespace Odin.Api.Endpoints.GeneticInspectionManagement.Models
{
    public class SubmitQpadmResultContract
    {
        public class Request : IValidatableObject
        {
            public required decimal Weight { get; set; }
            public required decimal StandardError { get; set; }
            public required decimal ZScore { get; set; }
            public required decimal PiValue { get; set; }
            public string RightSources { get; set; } = string.Empty;
            public string LeftSources { get; set; } = string.Empty;
            public List<int> PopulationIds { get; set; } = [];

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (Weight < 0 || Weight > 1)
                {
                    yield return new ValidationResult("Weight must be between 0 and 1.", [nameof(Weight)]);
                }

                if (StandardError < 0)
                {
                    yield return new ValidationResult("Standard error must be non-negative.", [nameof(StandardError)]);
                }
            }
        }

        public class Response
        {
            public int Id { get; set; }
            public int GeneticInspectionId { get; set; }
            public decimal Weight { get; set; }
            public decimal StandardError { get; set; }
            public decimal ZScore { get; set; }
            public decimal PiValue { get; set; }
            public string RightSources { get; set; } = string.Empty;
            public string LeftSources { get; set; } = string.Empty;
            public List<PopulationResponse> Populations { get; set; } = [];
        }
    }

    public class SubmitVahaduoResultContract
    {
        public class Response
        {
            public int Id { get; set; }
            public int GeneticInspectionId { get; set; }
        }
    }

    public class PopulationResponse
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int EraId { get; set; }
        public required string EraName { get; set; }
    }
}
