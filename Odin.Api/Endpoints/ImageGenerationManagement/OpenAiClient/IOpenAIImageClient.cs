namespace Odin.Api.Endpoints.ImageGenerationManagement;

/// <summary>
/// Thin seam over OpenAI image generation/editing (backed by the official <c>OpenAI</c> SDK) plus the
/// org usage/cost reporting (Administration API, Admin key). Behind an interface so integration tests can
/// substitute a fake and never call OpenAI. Implementations throw <see cref="OpenAIImageException"/> on
/// upstream failures.
/// </summary>
public interface IOpenAIImageClient
{
    /// <summary>Generate images from a text prompt (<c>/v1/images/generations</c>, gpt-image-2). Uses the project key.</summary>
    Task<OpenAIImageResult> GenerateAsync(OpenAIGenerateRequest request, CancellationToken cancellationToken = default);

    /// <summary>Generate images from a prompt + reference images (<c>/v1/images/edits</c>). Uses the project key.</summary>
    Task<OpenAIImageResult> EditAsync(OpenAIEditRequest request, CancellationToken cancellationToken = default);

    /// <summary>Image-generation usage counts over a time range (<c>/v1/organization/usage/images</c>). Uses the Admin key.</summary>
    Task<OpenAIImageUsageReport> GetImageUsageAsync(
        DateTimeOffset start, DateTimeOffset? end, string bucketWidth, CancellationToken cancellationToken = default);

    /// <summary>Daily costs over a time range (<c>/v1/organization/costs</c>). Uses the Admin key.</summary>
    Task<OpenAICostReport> GetCostsAsync(
        DateTimeOffset start, DateTimeOffset? end, CancellationToken cancellationToken = default);
}
