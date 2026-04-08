namespace Odin.Api.Configuration;

public sealed class PaddleOptions
{
    public const string SectionName = "Paddle";

    /// <summary>API key (bearer token) from Paddle → Developer Tools → Authentication.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Secret key used to verify <c>Paddle-Signature</c> on incoming webhooks.</summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>Base URL for the Paddle API (production: https://api.paddle.com, sandbox: https://sandbox-api.paddle.com).</summary>
    public string ApiBaseUrl { get; set; } = "https://sandbox-api.paddle.com";
}
