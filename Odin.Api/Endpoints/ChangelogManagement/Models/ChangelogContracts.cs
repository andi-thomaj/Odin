namespace Odin.Api.Endpoints.ChangelogManagement.Models;

public static class GetChangelogContract
{
    public class VersionResponse
    {
        public int Id { get; set; }
        public required string Version { get; set; }
        public required string Title { get; set; }
        public DateTime ReleasedAt { get; set; }
        public bool IsPublished { get; set; }
        public List<EntryResponse> Entries { get; set; } = [];
    }

    public class EntryResponse
    {
        public int Id { get; set; }
        public required string Type { get; set; }
        public required string Description { get; set; }
        public int DisplayOrder { get; set; }
    }
}

public static class CreateVersionContract
{
    public class Request
    {
        public required string Version { get; set; }
        public required string Title { get; set; }
        public DateTime ReleasedAt { get; set; }
        public bool IsPublished { get; set; }
    }

    public class Response
    {
        public int Id { get; set; }
        public required string Version { get; set; }
        public required string Title { get; set; }
        public DateTime ReleasedAt { get; set; }
        public bool IsPublished { get; set; }
    }
}

public static class UpdateVersionContract
{
    public class Request
    {
        public required string Version { get; set; }
        public required string Title { get; set; }
        public DateTime ReleasedAt { get; set; }
        public bool IsPublished { get; set; }
    }
}

public static class CreateEntryContract
{
    public class Request
    {
        public required string Type { get; set; }
        public required string Description { get; set; }
        public int DisplayOrder { get; set; }
    }

    public class Response
    {
        public int Id { get; set; }
        public int ChangelogVersionId { get; set; }
        public required string Type { get; set; }
        public required string Description { get; set; }
        public int DisplayOrder { get; set; }
    }
}

public static class UpdateEntryContract
{
    public class Request
    {
        public required string Type { get; set; }
        public required string Description { get; set; }
        public int DisplayOrder { get; set; }
    }
}
