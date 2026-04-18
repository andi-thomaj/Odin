namespace Odin.Api.Endpoints.G25AdmixtureFileManagement.Models;

public static class GetG25AdmixtureFileContract
{
    public class ListItem
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int G25RegionId { get; set; }
        public required string G25RegionName { get; set; }
        public int G25EthnicityId { get; set; }
        public required string G25EthnicityName { get; set; }
    }

    public class Response
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Content { get; set; }
        public int G25RegionId { get; set; }
        public required string G25RegionName { get; set; }
        public int G25EthnicityId { get; set; }
        public required string G25EthnicityName { get; set; }
    }
}

public static class CreateG25AdmixtureFileContract
{
    public class Request
    {
        public required string Name { get; set; }
        public required string Content { get; set; }
        public int G25RegionId { get; set; }
    }
}

public static class UpdateG25AdmixtureFileContract
{
    public class Request
    {
        public required string Name { get; set; }
        public required string Content { get; set; }
        public int G25RegionId { get; set; }
    }
}
