namespace Odin.Api.Endpoints.ImageGenerationManagement.Models;

/// <summary>
/// Response for an uploaded reference image, used by the upload (201), list, and get endpoints. The bytes
/// live in R2; <see cref="Response.Url"/> is the public URL.
/// </summary>
public static class ReferenceImageContract
{
    public sealed class Response
    {
        public int Id { get; set; }
        public required string OriginalFileName { get; set; }
        public required string Url { get; set; }
        public required string ContentType { get; set; }
        public long FileSizeBytes { get; set; }
        public string? Sha256 { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
