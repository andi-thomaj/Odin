namespace Odin.Api.Endpoints.UserManagement.Models
{
    public class GetErasContract
    {
        public class Response
        {
            public int Id { get; set; }
            public required string Name { get; set; }
            public string Description { get; set; }
            public List<PopulationItem> Populations { get; set; } = [];
        }

        public class PopulationItem
        {
            public int Id { get; set; }
            public required string Name { get; set; }
            public string Description { get; set; }
            public string IconFileName { get; set; }
            public MusicTrackItem? MusicTrack { get; set; }
        }

        public class MusicTrackItem
        {
            public int Id { get; set; }
            public required string Name { get; set; }
            public string FileName { get; set; }
            public int DisplayOrder { get; set; }
        }
    }
}
