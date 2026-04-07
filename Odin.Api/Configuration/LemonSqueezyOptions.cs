namespace Odin.Api.Configuration;

public sealed class LemonSqueezyOptions
{
    public const string SectionName = "LemonSqueezy";

    /// <summary>Bearer token for the Lemon Squeezy REST API.</summary>
    public string ApiKey { get; set; } = "";

    /// <summary>Numeric store ID visible in the LS dashboard URL.</summary>
    public string StoreId { get; set; } = "";

    /// <summary>Variant ID of the product the customer purchases.</summary>
    public string VariantId { get; set; } = "";

    /// <summary>HMAC signing secret from Settings → Webhooks (same value used to verify <c>X-Signature</c>).</summary>
    public string WebhookSigningSecret { get; set; } = "";
}
