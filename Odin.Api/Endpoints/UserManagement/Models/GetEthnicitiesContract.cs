namespace Odin.Api.Endpoints.UserManagement.Models
{
    public class GetEthnicitiesContract
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
}
