namespace Odin.Api.Endpoints.G25DistanceEraManagement.Models;

public static class GetG25DistanceEraContract
{
    public class DistanceFileSummary
    {
        public int Id { get; set; }
        public required string Title { get; set; }
    }

    public class Response
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public DistanceFileSummary? DistanceFile { get; set; }
    }
}

public static class CreateG25DistanceEraContract
{
    public class Request
    {
        public required string Name { get; set; }
    }
}

public static class UpdateG25DistanceEraContract
{
    public class Request
    {
        public required string Name { get; set; }
    }
}
