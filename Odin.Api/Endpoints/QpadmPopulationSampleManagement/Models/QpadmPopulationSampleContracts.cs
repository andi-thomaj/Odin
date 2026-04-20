namespace Odin.Api.Endpoints.QpadmPopulationSampleManagement.Models;

public static class QpadmResearchLinkDto
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
}

public static class GetQpadmPopulationSampleContract
{
    public class Response
    {
        public int Id { get; set; }
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
        public List<QpadmResearchLinkDto.Response> ResearchLinks { get; set; } = [];
    }

    public class PagedResponse
    {
        public List<Response> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}

public static class CreateQpadmPopulationSampleContract
{
    public class Request
    {
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
        public List<QpadmResearchLinkDto.CreateRequest>? ResearchLinks { get; set; }
    }

    public class Response
    {
        public int Id { get; set; }
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
        public List<QpadmResearchLinkDto.Response> ResearchLinks { get; set; } = [];
    }
}

public static class UpdateQpadmPopulationSampleContract
{
    public class Request
    {
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
    }
}
