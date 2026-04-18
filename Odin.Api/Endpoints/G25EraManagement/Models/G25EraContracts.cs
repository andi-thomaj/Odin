namespace Odin.Api.Endpoints.G25EraManagement.Models;

public static class GetG25EraContract
{
    public class DistanceFileSummary
    {
        public int Id { get; set; }
        public required string Title { get; set; }
    }

    public class Response
    {
        public int Id { get; set; }
        public required string Name { get; set; }
        public DistanceFileSummary? DistanceFile { get; set; }
    }
}

public static class CreateG25EraContract
{
    public class Request
    {
        public required string Name { get; set; }
    }
}

public static class UpdateG25EraContract
{
    public class Request
    {
        public required string Name { get; set; }
    }
}
