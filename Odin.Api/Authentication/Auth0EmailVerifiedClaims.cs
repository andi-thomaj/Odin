using System.Security.Claims;

namespace Odin.Api.Authentication;

/// <summary>Reads Auth0 <c>email_verified</c> from the JWT.</summary>
public static class Auth0EmailVerifiedClaims
{
    /// <summary>
    /// <c>null</c> when the JWT has no <c>email_verified</c> claim (common for Auth0 API access tokens);
    /// otherwise the claim value.
    /// </summary>
    /// <param name="principal">The authenticated principal.</param>
    /// <param name="customClaimType">
    /// Optional exact claim type to check first (e.g. the namespaced claim an Auth0 post-login Action
    /// stamps onto the access token, like <c>https://odin.ancestrify.io/email_verified</c>). Configure via
    /// <c>Jwt:EmailVerifiedClaim</c>. When the Action is live this lets the API read verification straight
    /// from the JWT and skip the Auth0 <c>/userinfo</c> round-trip entirely. The heuristic suffix match
    /// below remains as a fallback so behaviour is unchanged when the claim is absent.
    /// </param>
    public static bool? GetJwtEmailVerifiedBoolean(ClaimsPrincipal principal, string? customClaimType = null)
    {
        if (!string.IsNullOrWhiteSpace(customClaimType))
        {
            var custom = principal.FindFirst(c =>
                c.Type.Equals(customClaimType, StringComparison.OrdinalIgnoreCase));
            if (custom is not null)
                return string.Equals(custom.Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        foreach (var claim in principal.Claims)
        {
            if (claim.Type.Equals("email_verified", StringComparison.OrdinalIgnoreCase) ||
                claim.Type.EndsWith("/email_verified", StringComparison.Ordinal))
                return string.Equals(claim.Value, "true", StringComparison.OrdinalIgnoreCase);
        }

        return null;
    }

    /// <summary>Returns <c>"true"</c> or <c>"false"</c> from JWT only (missing claim → <c>"false"</c>).</summary>
    public static string GetAppEmailVerifiedValue(ClaimsPrincipal principal)
    {
        var b = GetJwtEmailVerifiedBoolean(principal);
        return b == true ? "true" : "false";
    }
}
