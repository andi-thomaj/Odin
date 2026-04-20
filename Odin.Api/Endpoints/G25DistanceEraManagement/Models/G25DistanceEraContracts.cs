namespace Odin.Api.Endpoints.G25DistanceEraManagement.Models;

public static class GetG25DistanceEraContract
{
    public class Response
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int SampleCount { get; set; }
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
