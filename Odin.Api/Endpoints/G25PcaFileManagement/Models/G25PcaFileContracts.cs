namespace Odin.Api.Endpoints.G25PcaFileManagement.Models;

public static class GetG25PcaFileContract
{
    public class ListItem
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public int G25DistanceEraId { get; set; }
        public required string G25DistanceEraName { get; set; }
    }

    public class Response
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public required string Content { get; set; }
        public int G25DistanceEraId { get; set; }
        public required string G25DistanceEraName { get; set; }
    }
}

public static class CreateG25PcaFileContract
{
    public class Request
    {
        public required string Title { get; set; }
        public required string Content { get; set; }
        public int G25DistanceEraId { get; set; }
    }
}

public static class UpdateG25PcaFileContract
{
    public class Request
    {
        public required string Title { get; set; }
        public required string Content { get; set; }
        public int G25DistanceEraId { get; set; }
    }
}

public static class GetG25PcaFilesByContinentsContract
{
    public class Response
    {
        public required IReadOnlyList<ContinentBundle> Continents { get; set; }
    }

    public class ContinentBundle
    {
        public int G25ContinentId { get; set; }
        public required string G25ContinentName { get; set; }
        public required IReadOnlyList<PcaFileEntry> PcaFiles { get; set; }
    }

    public class PcaFileEntry
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public int G25DistanceEraId { get; set; }
        public required string G25DistanceEraName { get; set; }
        public required string Content { get; set; }
    }
}
