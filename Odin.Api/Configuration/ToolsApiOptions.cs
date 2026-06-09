namespace Odin.Api.Configuration;

/// <summary>
/// Connection settings for the Python tools API (odin-tools-api), which hosts genetic-analysis
/// tools such as the Y-DNA clade finder. The .NET API proxies authenticated requests to it.
/// </summary>
public sealed class ToolsApiOptions
{
    public const string SectionName = "ToolsApi";

    /// <summary>Base URL of the odin-tools-api service, e.g. <c>http://localhost:8000</c>.</summary>
    public string BaseUrl { get; set; } = "";

    /// <summary>Shared secret sent as the <c>X-Api-Key</c> header. Leave empty to disable.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Request timeout in seconds (clade detection on large files can be slow).</summary>
    public int TimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Timeout in seconds for the merge pipeline client. The AADR merge runs synchronously on the
    /// tools API and can take many minutes on the HO panel, so this is far larger than
    /// <see cref="TimeoutSeconds"/>. Used by the dedicated merge HttpClient.
    /// </summary>
    public int MergeTimeoutSeconds { get; set; } = 1800;
}
