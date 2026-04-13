namespace Odin.Api.Endpoints.AdmixtureSavedFileManagement.Models;

public static class GetAdmixtureSavedFileContract
{
    public class Response
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public required string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class Summary
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public int LineCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

public static class CreateAdmixtureSavedFileContract
{
    public class Request
    {
        public required string Title { get; set; }
        public required string Content { get; set; }
    }
}

public static class UpdateAdmixtureSavedFileContract
{
    public class Request
    {
        public required string Title { get; set; }
        public required string Content { get; set; }
    }
}
