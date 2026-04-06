namespace Odin.Api.Services.Email;

/// <summary>Public URLs used in outbound emails (e.g. verification link to the SPA).</summary>
public sealed class AppPublicOptions
{
    public const string SectionName = "App";

    /// <summary>Frontend origin without trailing slash (e.g. https://dev.ancestrify.io).</summary>
    public string FrontendBaseUrl { get; set; } = "http://localhost:3000";
}
