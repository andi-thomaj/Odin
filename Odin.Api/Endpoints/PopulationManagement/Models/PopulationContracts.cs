namespace Odin.Api.Endpoints.PopulationManagement.Models;

public static class GetPopulationContract
{
    public class AdminResponse
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public string GeoJson { get; set; } = string.Empty;
        public string IconFileName { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int EraId { get; set; }
        public string EraName { get; set; } = string.Empty;
        public int MusicTrackId { get; set; }
        public string MusicTrackName { get; set; } = string.Empty;
        public bool HasGifAvatarImage { get; set; }
    }

    public class GifAvatarListItem
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public required string Version { get; set; }
        public bool HasVideo { get; set; }
    }
}

public static class CreatePopulationContract
{
    public class Request
    {
        public required string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public required string GeoJson { get; set; }
        public string IconFileName { get; set; } = string.Empty;
        public required string Color { get; set; }
        public required int EraId { get; set; }
        public required int MusicTrackId { get; set; }
    }
}

public static class UpdatePopulationContract
{
    public class Request
    {
        public required string Name { get; set; }
        public string Description { get; set; } = string.Empty;
        public required string GeoJson { get; set; }
        public string IconFileName { get; set; } = string.Empty;
        public required string Color { get; set; }
        public required int EraId { get; set; }
        public required int MusicTrackId { get; set; }
    }
}
