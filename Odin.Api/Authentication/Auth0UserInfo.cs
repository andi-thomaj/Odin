using System.Net.Http.Headers;
using System.Text.Json;

namespace Odin.Api.Authentication;

/// <summary>
/// Parsed Auth0 <c>/userinfo</c> response. Only the fields the API consumes are surfaced;
/// missing/blank claims become <c>null</c>.
/// </summary>
public sealed record Auth0UserInfoProfile(
    string? Email,
    bool? EmailVerified,
    string? Name,
    string? GivenName,
    string? FamilyName,
    string? Nickname);

/// <summary>
/// Calls Auth0 <c>/userinfo</c> with a bearer access token and parses the response. Used both by
/// <see cref="Middleware.RoleEnrichmentMiddleware"/> (to derive <c>email_verified</c> when the API token
/// omits the claim) and by JIT user provisioning (to populate a new <c>application_users</c> row).
/// </summary>
public static class Auth0UserInfoClient
{
    public static async Task<(Auth0UserInfoProfile? Profile, int StatusCode)> GetAsync(
        HttpClient httpClient,
        string authority,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        var userInfoUrl = $"{authority.TrimEnd('/')}/userinfo";
        using var request = new HttpRequestMessage(HttpMethod.Get, userInfoUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        using var response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var code = (int)response.StatusCode;
        if (!response.IsSuccessStatusCode)
            return (null, code);

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        bool? emailVerified = null;
        if (root.TryGetProperty("email_verified", out var ev))
        {
            emailVerified = ev.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => string.Equals(ev.GetString(), "true", StringComparison.OrdinalIgnoreCase),
                _ => null,
            };
        }

        var profile = new Auth0UserInfoProfile(
            Email: TryGetString(root, "email"),
            EmailVerified: emailVerified,
            Name: TryGetString(root, "name"),
            GivenName: TryGetString(root, "given_name"),
            FamilyName: TryGetString(root, "family_name"),
            Nickname: TryGetString(root, "nickname"));

        return (profile, code);
    }

    private static string? TryGetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.String)
            return null;
        var value = prop.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
