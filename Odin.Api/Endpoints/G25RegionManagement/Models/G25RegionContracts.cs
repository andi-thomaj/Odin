namespace Odin.Api.Endpoints.G25RegionManagement.Models;

public static class GetG25RegionContract
{
    public class Response
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public int G25EthnicityId { get; set; }
        public required string G25EthnicityName { get; set; }
        public bool HasAdmixtureFile { get; set; }
    }
}

public static class CreateG25RegionContract
{
    public class Request
    {
        public required string Name { get; set; }
        public int G25EthnicityId { get; set; }
    }
}

public static class UpdateG25RegionContract
{
    public class Request
    {
        public required string Name { get; set; }
        public int G25EthnicityId { get; set; }
    }
}
