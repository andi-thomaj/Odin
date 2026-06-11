namespace Odin.Api.Endpoints.QpadmPopulationPanelSampleManagement.Models;

/// <summary>One population currently linked to a panel sample.</summary>
public static class LinkedPopulationDto
{
    public class Response
    {
        public int PopulationId { get; set; }
        public required string PopulationName { get; set; }
    }
}

public static class GetPanelSampleLinksContract
{
    /// <summary>A single (sample → population) link row for a panel.</summary>
    public class Response
    {
        public required string SampleId { get; set; }
        public int PopulationId { get; set; }
        public required string PopulationName { get; set; }
    }
}

/// <summary>Replace the full set of populations linked to one panel sample.</summary>
public static class SetSamplePopulationsContract
{
    public class Request
    {
        public required string Panel { get; set; }
        public required string SampleId { get; set; }
        public List<int> PopulationIds { get; set; } = [];
    }

    public class Response
    {
        public required string Panel { get; set; }
        public required string SampleId { get; set; }
        public List<LinkedPopulationDto.Response> Populations { get; set; } = [];
    }
}

/// <summary>Assign populations to many panel samples at once (grid multi-select).</summary>
public static class BulkAssignSamplePopulationsContract
{
    public class Request
    {
        public required string Panel { get; set; }
        public List<string> SampleIds { get; set; } = [];
        public List<int> PopulationIds { get; set; } = [];

        /// <summary><c>add</c> merges into existing links; <c>replace</c> overwrites each sample's set.</summary>
        public string Mode { get; set; } = "add";
    }

    public class Response
    {
        public int SamplesAffected { get; set; }
        public int LinksAdded { get; set; }
        public int LinksRemoved { get; set; }
    }
}
