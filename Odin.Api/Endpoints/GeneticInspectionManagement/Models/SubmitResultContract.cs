using System.ComponentModel.DataAnnotations;

namespace Odin.Api.Endpoints.GeneticInspectionManagement.Models
{
    public class PopulationPercentageItem
    {
        public int PopulationId { get; set; }
        public decimal Percentage { get; set; }
        public decimal StandardError { get; set; }
        public decimal ZScore { get; set; }
    }

    public class EraGroupItem
    {
        public int EraId { get; set; }
        public decimal PiValue { get; set; }
        public string RightSources { get; set; } = string.Empty;
        public string LeftSources { get; set; } = string.Empty;
        public List<PopulationPercentageItem> Populations { get; set; } = [];
    }

    public class SubmitQpadmResultContract
    {
        public class Request : IValidatableObject
        {
            public List<EraGroupItem> EraGroups { get; set; } = [];
            public string? OrderStatus { get; set; }
            public string? PaternalHaplogroup { get; set; }
            public IFormFile? MergedRawDataFile { get; set; }

            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                if (PaternalHaplogroup?.Length > 50)
                    yield return new ValidationResult(
                        "Paternal haplogroup must not exceed 50 characters.",
                        [nameof(PaternalHaplogroup)]);

                if (EraGroups.Count == 0)
                {
                    yield return new ValidationResult(
                        "At least one era group is required.",
                        [nameof(EraGroups)]);
                }

                foreach (var group in EraGroups)
                {
                    if (group.PiValue < 0 || group.PiValue > 9.00m)
                    {
                        yield return new ValidationResult(
                            $"Pi value for era {group.EraId} must be between 0 and 9.00.",
                            [nameof(EraGroups)]);
                    }

                    var totalPercentage = group.Populations.Sum(p => p.Percentage);
                    if (totalPercentage != 100)
                    {
                        yield return new ValidationResult(
                            $"Population percentages for era {group.EraId} must total exactly 100%. Current total: {totalPercentage}%.",
                            [nameof(EraGroups)]);
                    }

                    foreach (var pop in group.Populations)
                    {
                        if (pop.Percentage < 0 || pop.Percentage > 100)
                        {
                            yield return new ValidationResult(
                                $"Percentage for population {pop.PopulationId} must be between 0 and 100.",
                                [nameof(EraGroups)]);
                        }

                        if (pop.StandardError < 0 || pop.StandardError > 9.00m)
                        {
                            yield return new ValidationResult(
                                $"Standard error for population {pop.PopulationId} must be between 0 and 9.00.",
                                [nameof(EraGroups)]);
                        }

                        if (pop.ZScore > 9.99m)
                        {
                            yield return new ValidationResult(
                                $"Z-Score for population {pop.PopulationId} must not exceed 9.99.",
                                [nameof(EraGroups)]);
                        }
                    }
                }
            }
        }

        public class Response
        {
            public int Id { get; set; }
            public int GeneticInspectionId { get; set; }
            public List<EraGroupResponse> EraGroups { get; set; } = [];
        }
    }

    public class EraGroupResponse
    {
        public int EraId { get; set; }
        public required string EraName { get; set; }
        public decimal PiValue { get; set; }
        public string RightSources { get; set; } = string.Empty;
        public string LeftSources { get; set; } = string.Empty;
        public List<PopulationResponse> Populations { get; set; } = [];
    }

    public class PopulationResponse
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public decimal Percentage { get; set; }
        public decimal StandardError { get; set; }
        public decimal ZScore { get; set; }
    }
}
