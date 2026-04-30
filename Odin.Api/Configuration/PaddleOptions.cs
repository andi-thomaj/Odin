namespace Odin.Api.Configuration;

public sealed class PaddleOptions
{
    public const string SectionName = "Paddle";

    /// <summary>API key (bearer token) from Paddle → Developer Tools → Authentication. Format: <c>pdl_*_apikey_*</c>.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Secret key used to verify <c>Paddle-Signature</c> on incoming webhooks.</summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>Base URL for the Paddle API (production: https://api.paddle.com, sandbox: https://sandbox-api.paddle.com).</summary>
    public string ApiBaseUrl { get; set; } = "https://sandbox-api.paddle.com";

    /// <summary>Value sent in the <c>Paddle-Version</c> header to pin the API version. Defaults to <c>1</c>.</summary>
    public string ApiVersion { get; set; } = "1";

    /// <summary>Per-request timeout for outbound calls to Paddle.</summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>Maximum retry attempts for transient failures (5xx, 429). Total attempts = <c>1 + MaxRetries</c>.</summary>
    public int MaxRetries { get; set; } = 3;
}
