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
            public List<int> CountryIds { get; set; } = [];
            public List<int> GeneticInspectionRegionIds { get; set; } = [];
            public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
            {
                throw new NotImplementedException();
            }
        }
    }
}
