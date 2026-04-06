namespace Odin.Api.Endpoints.G25AncientManagement.Models;

public static class GetG25AncientContract
{
    public class Response
    {
        public int Id { get; set; }
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
    }

    public class PagedResponse
    {
        public List<Response> Items { get; set; } = [];
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}

public static class CreateG25AncientContract
{
    public class Request
    {
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
    }

    public class Response
    {
        public int Id { get; set; }
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
    }
}

public static class UpdateG25AncientContract
{
    public class Request
    {
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
    }
}
