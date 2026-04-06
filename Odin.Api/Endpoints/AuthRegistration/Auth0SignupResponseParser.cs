using System.Text.Json;

namespace Odin.Api.Endpoints.AuthRegistration;

internal static class Auth0SignupResponseParser
{
    /// <summary>Parses Auth0 <c>/dbconnections/signup</c> success JSON into the JWT <c>sub</c> form.</summary>
    public static string? ParseIdentityIdFromSuccessBody(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("user_id", out var userId))
        {
            var s = userId.GetString();
            if (!string.IsNullOrWhiteSpace(s))
                return s.Trim();
        }

        if (root.TryGetProperty("_id", out var idEl))
        {
            var id = idEl.GetString();
            if (string.IsNullOrWhiteSpace(id))
                return null;

            return id.StartsWith("auth0|", StringComparison.Ordinal)
                ? id
                : $"auth0|{id}";
        }

        return null;
    }
}
