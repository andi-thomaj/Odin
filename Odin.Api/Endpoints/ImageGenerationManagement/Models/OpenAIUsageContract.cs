namespace Odin.Api.Endpoints.ImageGenerationManagement.Models;

/// <summary>
/// Response for <c>GET api/admin/images/usage</c> — OpenAI API usage + cost for the image model, read from
/// the Administration API with the Admin key. gpt-image-2 is a GPT model, so its usage is reported by the
/// <b>completions</b> usage endpoint (token + request counts), filtered to the model — NOT the legacy
/// <c>/usage/images</c> endpoint (which only covers the old DALL-E image API and reads 0 for gpt-image-2).
/// Daily <b>costs</b> come from <c>/organization/costs</c> and are org-wide and lag ~a day.
/// <see cref="Response.OpenAiError"/> is set (and the data left empty) when the Admin key is missing or a
/// call fails, so the page shows a clear message instead of erroring.
/// </summary>
public static class OpenAIUsageContract
{
    public sealed class Response
    {
        /// <summary>The image model these stats cover (e.g. gpt-image-2).</summary>
        public required string Model { get; set; }

        public List<UsageBucket> UsageBuckets { get; set; } = [];
        public long TotalRequests { get; set; }
        public long TotalInputTokens { get; set; }
        public long TotalOutputTokens { get; set; }

        public List<CostBucket> CostBuckets { get; set; } = [];

        /// <summary>Authoritative settled cost from OpenAI's <c>/costs</c> endpoint — org-wide, lags ~a day.</summary>
        public decimal TotalCostUsd { get; set; }

        /// <summary>Live estimate = token counts × the configured gpt-image-2 rates. Available immediately,
        /// before <see cref="TotalCostUsd"/> settles. Approximate (input text/image split isn't broken out).</summary>
        public decimal EstimatedCostUsd { get; set; }

        public required string Currency { get; set; }

        public string? OpenAiError { get; set; }
    }

    public sealed class UsageBucket
    {
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public long Requests { get; set; }
        public long InputTokens { get; set; }
        public long OutputTokens { get; set; }
    }

    public sealed class CostBucket
    {
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public decimal AmountUsd { get; set; }
    }
}
