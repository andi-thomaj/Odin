using System.Net;
using Odin.Api.Endpoints.ImageGenerationManagement;

namespace Odin.Api.IntegrationTests.Fakers;

/// <summary>
/// Test double for <see cref="IOpenAIImageClient"/> so CI never calls OpenAI. Returns canned 1×1 PNGs +
/// usage for normal prompts, and throws for sentinel prompts so the endpoint error mapping is exercised:
/// "moderation-boom" → moderation block (422), "rate-boom" → rate limit (429).
/// </summary>
public sealed class FakeOpenAIImageClient : IOpenAIImageClient
{
    // 1×1 transparent PNG (valid magic bytes).
    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M8AAAMBAQDJ/pLvAAAAAElFTkSuQmCC");

    public Task<OpenAIImageResult> GenerateAsync(OpenAIGenerateRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(Build(request.Prompt, request.Parameters.N));

    public Task<OpenAIImageResult> EditAsync(OpenAIEditRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(Build(request.Prompt, request.Parameters.N));

    private static OpenAIImageResult Build(string prompt, int n)
    {
        if (prompt.Contains("moderation-boom", StringComparison.OrdinalIgnoreCase))
            throw new OpenAIImageException(
                HttpStatusCode.BadRequest, "Your request was rejected by the safety system.",
                isModeration: true, code: "moderation_blocked");

        if (prompt.Contains("rate-boom", StringComparison.OrdinalIgnoreCase))
            throw new OpenAIImageException(
                HttpStatusCode.TooManyRequests, "Rate limit exceeded.", code: "rate_limit_exceeded");

        var images = Enumerable.Range(0, Math.Max(1, n))
            .Select(_ => new OpenAIGeneratedImageBytes(OnePixelPng, $"revised: {prompt}"))
            .ToList();

        return new OpenAIImageResult(images, new OpenAIImageUsage(10, 20, 30));
    }

    public Task<OpenAICompletionsUsageReport> GetCompletionsUsageAsync(
        string model, DateTimeOffset start, DateTimeOffset? end, string bucketWidth, CancellationToken cancellationToken = default)
    {
        var bucket = new OpenAIUsageBucket(start.ToUnixTimeSeconds(), start.ToUnixTimeSeconds() + 86_400, 2, 50, 100);
        return Task.FromResult(new OpenAICompletionsUsageReport([bucket], 2, 50, 100));
    }

    public Task<OpenAICostReport> GetCostsAsync(
        DateTimeOffset start, DateTimeOffset? end, CancellationToken cancellationToken = default)
    {
        var bucket = new OpenAICostBucket(start.ToUnixTimeSeconds(), start.ToUnixTimeSeconds() + 86_400, 0.12m);
        return Task.FromResult(new OpenAICostReport([bucket], 0.12m, "usd"));
    }
}
