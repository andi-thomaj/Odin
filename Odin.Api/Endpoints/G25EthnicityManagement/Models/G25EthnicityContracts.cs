namespace Odin.Api.Endpoints.G25EthnicityManagement.Models;

public static class GetG25EthnicityContract
{
    public class Response
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int G25RegionId { get; set; }
        public string? G25RegionName { get; set; }
    }
}

public static class GetG25EthnicityAdminContract
{
    public class Response
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int G25RegionId { get; set; }
        public string? G25RegionName { get; set; }
        public bool HasAdmixtureFile { get; set; }
    }
}

public static class CreateG25EthnicityContract
{
    public class Request
    {
        public required string Name { get; set; }
        public int G25RegionId { get; set; }
    }
}

public static class UpdateG25EthnicityContract
{
    public class Request
    {
        public required string Name { get; set; }
        public int G25RegionId { get; set; }
    }
}
