using System.Security.Cryptography;
using System.Text;

namespace Odin.Api.Services.LemonSqueezy;

/// <summary>
/// Verifies <c>X-Signature</c> per <see href="https://docs.lemonsqueezy.com/help/webhooks/signing-requests">Lemon Squeezy signing requests</see>.
/// </summary>
public static class LemonSqueezyWebhookSignatureVerifier
{
    public static bool IsValid(byte[] rawBody, string signingSecret, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(signingSecret))
            return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(signingSecret));
        var hash = hmac.ComputeHash(rawBody);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        var expectedUtf8 = Encoding.UTF8.GetBytes(hex);
        var actualUtf8 = Encoding.UTF8.GetBytes(signatureHeader);

        return expectedUtf8.Length == actualUtf8.Length
               && CryptographicOperations.FixedTimeEquals(expectedUtf8, actualUtf8);
    }
}
