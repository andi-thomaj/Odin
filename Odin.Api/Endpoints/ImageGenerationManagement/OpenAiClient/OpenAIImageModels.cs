namespace Odin.Api.Endpoints.ImageGenerationManagement;

// Internal request/result shapes for the OpenAI image client. These are NOT the public API contracts
// (those live in Models/); they decouple the service + SDK call from the HTTP surface.

/// <summary>Effective gpt-image-2 parameters after merging request overrides over the persisted defaults.</summary>
public sealed record OpenAIImageParameters(
    string Model,
    string Size,
    string Quality,
    string Background,
    string OutputFormat,
    int? OutputCompression,
    string Moderation,
    int N,
    string? EndUserId);

/// <summary>A reference (input) image for an edit request, already validated and read into memory.</summary>
public sealed record OpenAIReferenceImage(byte[] Bytes, string ContentType, string FileName);

public sealed record OpenAIGenerateRequest(string Prompt, OpenAIImageParameters Parameters);

public sealed record OpenAIEditRequest(
    string Prompt,
    OpenAIImageParameters Parameters,
    IReadOnlyList<OpenAIReferenceImage> Images,
    OpenAIReferenceImage? Mask,
    string? InputFidelity);

/// <summary>One produced image (raw bytes) plus gpt-image-2's optional revised prompt.</summary>
public sealed record OpenAIGeneratedImageBytes(byte[] Bytes, string? RevisedPrompt);

public sealed record OpenAIImageUsage(long? InputTokens, long? OutputTokens, long? TotalTokens);

public sealed record OpenAIImageResult(IReadOnlyList<OpenAIGeneratedImageBytes> Images, OpenAIImageUsage? Usage);

// ── Administration API (usage / costs) ────────────────────────────────────────────────────────────

public sealed record OpenAIUsageBucket(long StartTime, long EndTime, long Images, long Requests);

public sealed record OpenAIImageUsageReport(IReadOnlyList<OpenAIUsageBucket> Buckets, long TotalImages, long TotalRequests);

public sealed record OpenAICostBucket(long StartTime, long EndTime, decimal AmountUsd);

public sealed record OpenAICostReport(IReadOnlyList<OpenAICostBucket> Buckets, decimal TotalAmountUsd, string Currency);
