namespace Odin.Api.Endpoints.G25AdmixtureEraManagement.Models;

public static class GetG25AdmixtureEraContract
{
    public class Response
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }
}

public static class CreateG25AdmixtureEraContract
{
    public class Request
    {
        public required string Name { get; set; }
    }
}

public static class UpdateG25AdmixtureEraContract
{
    public class Request
    {
        public required string Name { get; set; }
    }
}
