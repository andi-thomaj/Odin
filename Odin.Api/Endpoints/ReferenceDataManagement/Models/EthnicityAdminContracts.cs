namespace Odin.Api.Endpoints.ReferenceDataManagement.Models;

public static class GetEthnicityAdminContract
{
    public class Response
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public List<RegionItem> Regions { get; set; } = [];
    }

    public class RegionItem
    {
        public int Id { get; set; }
        public required string Name { get; set; }
    }
}

public static class CreateEthnicityContract
{
    public class Request
    {
        public required string Name { get; set; }
    }
}

public static class UpdateEthnicityContract
{
    public class Request
    {
        public required string Name { get; set; }
    }
}

public static class CreateRegionContract
{
    public class Request
    {
        public required string Name { get; set; }
    }
}

public static class UpdateRegionContract
{
    public class Request
    {
        public required string Name { get; set; }
    }
}
