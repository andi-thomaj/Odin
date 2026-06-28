namespace Odin.Api.Endpoints.ImageGenerationManagement.Models;

/// <summary>
/// Response for <c>GET api/admin/images/usage</c> — image-generation usage counts and daily costs from
/// OpenAI's Administration API (read with the Admin key). Costs are org-wide and lag (finalised ~next day).
/// </summary>
public static class OpenAIUsageContract
{
    public sealed class Response
    {
        public List<UsageBucket> UsageBuckets { get; set; } = [];
        public long TotalImages { get; set; }
        public long TotalRequests { get; set; }
        public List<CostBucket> CostBuckets { get; set; } = [];
        public decimal TotalCostUsd { get; set; }
        public required string Currency { get; set; }
    }

    public sealed class UsageBucket
    {
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public long Images { get; set; }
        public long Requests { get; set; }
    }

    public sealed class CostBucket
    {
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public decimal AmountUsd { get; set; }
    }
}
