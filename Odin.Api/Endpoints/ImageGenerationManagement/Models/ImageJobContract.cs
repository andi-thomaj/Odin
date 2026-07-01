namespace Odin.Api.Endpoints.ImageGenerationManagement.Models;

/// <summary>
/// Canonical representation of an image-generation job. Returned by the generate endpoints (200 with
/// images for a completed sync job, 202 for an accepted async job) and by the job-status (poll) endpoint.
/// </summary>
public static class ImageJobContract
{
    public sealed class Response
    {
        public required Guid JobId { get; set; }
        public required string Mode { get; set; }
        public required string Status { get; set; }
        public bool IsAsync { get; set; }
        public required string Prompt { get; set; }
        public string? RevisedPrompt { get; set; }
        public required string Model { get; set; }
        public required string Size { get; set; }
        public required string Quality { get; set; }
        public required string Background { get; set; }
        public required string OutputFormat { get; set; }
        public int? OutputCompression { get; set; }
        public required string Moderation { get; set; }
        public int N { get; set; }
        public int[]? ReferenceImageIds { get; set; }
        public long? UsageInputTokens { get; set; }
        public long? UsageOutputTokens { get; set; }
        public long? UsageTotalTokens { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public List<Image> Images { get; set; } = [];

        public sealed class Image
        {
            public int Id { get; set; }
            public int BatchIndex { get; set; }
            public required string Url { get; set; }
            public required string ContentType { get; set; }
            public long FileSizeBytes { get; set; }
        }
    }
}
