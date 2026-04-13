namespace Odin.Api.Endpoints.G25SavedCoordinateManagement.Models;

public static class GetG25SavedCoordinateContract
{
    public class Response
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public required string RawInput { get; set; }
        public bool Scaling { get; set; }
        public required string AddMode { get; set; }
        public string? CustomName { get; set; }
        public required string ViewId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

public static class CreateG25SavedCoordinateContract
{
    public class Request
    {
        public required string Title { get; set; }
        public required string RawInput { get; set; }
        public bool Scaling { get; set; }
        public required string AddMode { get; set; }
        public string? CustomName { get; set; }
        public required string ViewId { get; set; }
    }
}

public static class UpdateG25SavedCoordinateContract
{
    public class Request
    {
        public required string Title { get; set; }
        public required string RawInput { get; set; }
        public bool Scaling { get; set; }
        public required string AddMode { get; set; }
        public string? CustomName { get; set; }
        public required string ViewId { get; set; }
    }
}
