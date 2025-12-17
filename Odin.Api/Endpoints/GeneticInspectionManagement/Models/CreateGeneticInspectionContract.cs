namespace Odin.Api.Endpoints.GeneticInspectionManagement.Models
{
    public class CreateGeneticInspectionContract
    {
        public class Request
        {
            public required string FirstName { get; set; }
            public string? MiddleName { get; set; }
            public required string LastName { get; set; }
            public required int RawGeneticFileId { get; set; }
            public List<int> CountryIds { get; set; } = [];
            public List<int> GeneticInspectionRegionIds { get; set; } = [];
        }

        public class Response
        {
            public int Id { get; set; }
            public string FirstName { get; set; } = null!;
            public string? MiddleName { get; set; }
            public string LastName { get; set; } = null!;
            public int RawGeneticFileId { get; set; }
            public List<int> CountryIds { get; set; } = [];
            public List<int> GeneticInspectionRegionIds { get; set; } = [];
        }
    }
}
