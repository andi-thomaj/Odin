using System.Net.Http.Headers;
using System.Text.Json;

namespace Odin.Api.Authentication;

/// <summary>
/// When the access token omits <c>email_verified</c>, Auth0 still exposes it on <c>/userinfo</c> for that bearer token
/// when the token is accepted by that endpoint (typically tokens that include the userinfo audience / openid scopes).
/// </summary>
public static class Auth0UserInfoEmailVerified
{
    /// <summary>Returns <c>app_email_verified</c> string and HTTP status from userinfo (for diagnostics).</summary>
    public static async Task<(string AppValue, int StatusCode)> GetAppEmailVerifiedWithStatusAsync(
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
            return ("false", code);

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.TryGetProperty("email_verified", out var ev))
            return (ev.ValueKind == JsonValueKind.True ? "true" : "false", code);

        return ("false", code);
    }

    public static async Task<string> GetAppEmailVerifiedAsync(
        HttpClient httpClient,
        string authority,
        string bearerToken,
        CancellationToken cancellationToken)
    {
        var (value, _) = await GetAppEmailVerifiedWithStatusAsync(
            httpClient,
            authority,
            bearerToken,
            cancellationToken).ConfigureAwait(false);
        return value;
    }
}
