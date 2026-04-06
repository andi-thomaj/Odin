using System.Security.Claims;

namespace Odin.Api.Authentication;

/// <summary>Reads Auth0 <c>email_verified</c> from the JWT (single source of truth).</summary>
public static class Auth0EmailVerifiedClaims
{
    /// <summary>Returns <c>"true"</c> or <c>"false"</c> for <see cref="AppClaimTypes.EmailVerified"/> policies.</summary>
    public static string GetAppEmailVerifiedValue(ClaimsPrincipal principal)
    {
        foreach (var claim in principal.Claims)
        {
            if (claim.Type.Equals("email_verified", StringComparison.OrdinalIgnoreCase) ||
                claim.Type.EndsWith("/email_verified", StringComparison.Ordinal))
                return string.Equals(claim.Value, "true", StringComparison.OrdinalIgnoreCase)
                    ? "true"
                    : "false";
        }

        return "false";
    }
}
