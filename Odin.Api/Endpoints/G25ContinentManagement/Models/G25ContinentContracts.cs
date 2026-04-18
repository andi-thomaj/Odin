namespace Odin.Api.Endpoints.G25ContinentManagement.Models;

public static class GetG25ContinentContract
{
    public class Response
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int EthnicityCount { get; set; }
    }
}

public static class CreateG25ContinentContract
{
    public class Request
    {
        public required string Name { get; set; }
    }
}

public static class UpdateG25ContinentContract
{
    public class Request
    {
        public required string Name { get; set; }
    }
}
