namespace Odin.Api.Configuration;

/// <summary>
/// Credentials and connection settings for OpenAI image generation (model <c>gpt-image-2</c> via the
/// official <c>OpenAI</c> .NET SDK). Two keys are kept deliberately separate:
/// <list type="bullet">
///   <item><see cref="ApiKey"/> — a <b>project/API key</b> used ONLY to generate/edit images.</item>
///   <item><see cref="AdminApiKey"/> — an <b>organization Admin key</b> used ONLY for the usage/cost
///   reporting endpoints (OpenAI Administration API). It cannot — and must never — be used to generate
///   images, and an image key cannot read the Administration API.</item>
/// </list>
/// Secret values come from user-secrets locally (<c>dotnet user-secrets set "OpenAI:ApiKey" …</c>) and
/// Coolify environment variables in production (never appsettings.json). Never log or echo either key.
/// </summary>
public sealed class OpenAIOptions
{
    public const string SectionName = "OpenAI";

    /// <summary>Project/API key (<c>sk-proj-…</c>) used for image generation and edits. Never log this value.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Organization Admin key (<c>sk-admin-…</c>) used ONLY for usage/cost reporting. Never log this value.</summary>
    public string AdminApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Base URL for the OpenAI API. Used as the SDK endpoint override and the base address of the
    /// Administration HttpClient. Defaults to the public API; override for a proxy/Azure gateway.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.openai.com";

    /// <summary>Image model id. gpt-image-2 is the current model; kept configurable for future bumps.</summary>
    public string GenerationModel { get; set; } = "gpt-image-2";

    /// <summary>
    /// Per-request timeout in seconds for the generation/edit calls. gpt-image-2 at high quality (or with
    /// several images) is slow (high quality runs a four-stage pipeline ~30–50× slower than low), so this is
    /// generous; the synchronous endpoint additionally caps the HTTP request at 5 minutes and heavy work
    /// should use the async (Hangfire) mode.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 180;
}
