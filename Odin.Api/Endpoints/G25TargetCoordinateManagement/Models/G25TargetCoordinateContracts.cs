namespace Odin.Api.Endpoints.G25TargetCoordinateManagement.Models;

public static class GetG25TargetCoordinateContract
{
    public class Response
    {
        public int Id { get; set; }
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class Summary
    {
        public int Id { get; set; }
        public required string Label { get; set; }
        public int LineCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

public static class CreateG25TargetCoordinateContract
{
    public class Request
    {
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
    }
}

public static class UpdateG25TargetCoordinateContract
{
    public class Request
    {
        public required string Label { get; set; }
        public required string Coordinates { get; set; }
    }
}
