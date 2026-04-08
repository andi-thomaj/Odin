using System.Security.Cryptography;
using System.Text;

namespace Odin.Api.Services.Paddle;

/// <summary>
/// Verifies <c>Paddle-Signature</c> per <see href="https://developer.paddle.com/webhooks/signature-verification">Paddle webhook verification</see>.
/// The header format is <c>ts=TIMESTAMP;h1=HASH</c> where HASH = HMAC-SHA256(secret, "TIMESTAMP:BODY").
/// </summary>
public static class PaddleWebhookSignatureVerifier
{
    private const int MaxTimestampDriftSeconds = 300;

    public static bool IsValid(byte[] rawBody, string webhookSecret, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(signatureHeader) || string.IsNullOrEmpty(webhookSecret))
            return false;

        if (!TryParseHeader(signatureHeader, out var timestamp, out var hash))
            return false;

        if (!long.TryParse(timestamp, out var ts))
            return false;

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - ts) > MaxTimestampDriftSeconds)
            return false;

        var signedPayload = $"{timestamp}:{Encoding.UTF8.GetString(rawBody)}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
        var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload));
        var expectedHex = Convert.ToHexString(computed).ToLowerInvariant();

        var expectedUtf8 = Encoding.UTF8.GetBytes(expectedHex);
        var actualUtf8 = Encoding.UTF8.GetBytes(hash);

        return expectedUtf8.Length == actualUtf8.Length
               && CryptographicOperations.FixedTimeEquals(expectedUtf8, actualUtf8);
    }

    private static bool TryParseHeader(string header, out string timestamp, out string hash)
    {
        timestamp = "";
        hash = "";

        foreach (var part in header.Split(';'))
        {
            if (part.StartsWith("ts=", StringComparison.Ordinal))
                timestamp = part[3..];
            else if (part.StartsWith("h1=", StringComparison.Ordinal))
                hash = part[3..];
        }

        return !string.IsNullOrEmpty(timestamp) && !string.IsNullOrEmpty(hash);
    }
}
