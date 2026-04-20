namespace Odin.Api.Endpoints.G25PopulationSampleManagement.Models;

public static class ResearchLinkDto
{
    public class Response
    {
        public int Id { get; set; }
        public required string Label { get; set; }
        public required string Link { get; set; }
    }

    public class CreateRequest
    {
        public required string Label { get; set; }
        public required string Link { get; set; }
    }

    /// <summary>
    /// Upsert shape for Update: links with an Id are updated; links without an Id are inserted.
    /// Any existing link whose Id is absent from the incoming list is deleted.
    /// </summary>
    public class UpdateRequest
    {
        public int? Id { get; set; }
        public required string Label { get; set; }
        public required string Link { get; set; }
    }
}

public class G25AdmixtureEraSummaryDto
{
    public int Id { get; set; }
    public required string Name { get; set; }
}

public static class GetG25PopulationSampleContract
{
    public class Response
    {
        public int Id { get; set; }
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
        public required string Ids { get; set; }
        public int? G25AdmixtureEraId { get; set; }
        public G25AdmixtureEraSummaryDto? G25AdmixtureEra { get; set; }
        public List<ResearchLinkDto.Response> ResearchLinks { get; set; } = [];
    }

    public class PagedResponse
    {
        public List<Response> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}

public static class CreateG25PopulationSampleContract
{
    public class Request
    {
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
        public string? Ids { get; set; }
        public int? G25AdmixtureEraId { get; set; }
        public List<ResearchLinkDto.CreateRequest>? ResearchLinks { get; set; }
    }

    public class Response
    {
        public int Id { get; set; }
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
        public required string Ids { get; set; }
        public int? G25AdmixtureEraId { get; set; }
        public G25AdmixtureEraSummaryDto? G25AdmixtureEra { get; set; }
        public List<ResearchLinkDto.Response> ResearchLinks { get; set; } = [];
    }
}

public static class UpdateG25PopulationSampleContract
{
    public class Request
    {
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
        public string? Ids { get; set; }
        public int? G25AdmixtureEraId { get; set; }
        public List<ResearchLinkDto.UpdateRequest>? ResearchLinks { get; set; }
    }
}
