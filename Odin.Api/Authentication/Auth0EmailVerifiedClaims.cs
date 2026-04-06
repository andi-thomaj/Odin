using System.Security.Claims;

namespace Odin.Api.Authentication;

/// <summary>Reads Auth0 <c>email_verified</c> from the JWT.</summary>
public static class Auth0EmailVerifiedClaims
{
    /// <summary>
    /// <c>null</c> when the JWT has no <c>email_verified</c> claim (common for Auth0 API access tokens);
    /// otherwise the claim value.
    /// </summary>
    public static bool? GetJwtEmailVerifiedBoolean(ClaimsPrincipal principal)
    {
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
