using System.Net;

namespace Odin.Api.Endpoints.ImageGenerationManagement;

/// <summary>
/// Raised when an OpenAI image call fails, preserving the upstream status code and a user-safe detail.
/// <see cref="IsModeration"/> flags a prompt rejected by OpenAI's safety system (<c>moderation_blocked</c>),
/// which the endpoint surfaces as 422 and which must NOT be retried (the same prompt re-blocks).
/// </summary>
public sealed class OpenAIImageException(
    HttpStatusCode statusCode,
    string detail,
    bool isModeration = false,
    string? code = null) : Exception(detail)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
    public string Detail { get; } = detail;
    public bool IsModeration { get; } = isModeration;
    public string? Code { get; } = code;

    /// <summary>True for failures worth retrying (rate-limit / transient upstream errors); 429 and 5xx.</summary>
    public bool IsTransient => (int)StatusCode == 429 || (int)StatusCode >= 500;
}
