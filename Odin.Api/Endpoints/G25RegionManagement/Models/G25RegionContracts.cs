namespace Odin.Api.Endpoints.G25RegionManagement.Models;

public static class GetG25RegionContract
{
    public class Response
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }
}

public static class CreateG25RegionContract
{
    public class Request
    {
        public required string Name { get; set; }
    }
}

public static class UpdateG25RegionContract
{
    public class Request
    {
        public required string Name { get; set; }
    }
}
