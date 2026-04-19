namespace Odin.Api.Endpoints.G25DistanceFileManagement.Models;

public static class GetG25DistanceFileContract
{
    public class ListItem
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public int G25DistanceEraId { get; set; }
        public string? G25DistanceEraName { get; set; }
    }

    public class Response
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public required string Content { get; set; }
        public int G25DistanceEraId { get; set; }
        public string? G25DistanceEraName { get; set; }
    }
}

public static class CreateG25DistanceFileContract
{
    public class Request
    {
        public required string Title { get; set; }
        public required string Content { get; set; }
        public int G25DistanceEraId { get; set; }
    }
}

public static class UpdateG25DistanceFileContract
{
    public class Request
    {
        public required string Title { get; set; }
        public required string Content { get; set; }
        public int G25DistanceEraId { get; set; }
    }
}
