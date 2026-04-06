namespace Odin.Api.Authentication;

/// <summary>Custom claims added after JWT validation (see <see cref="Middleware.RoleEnrichmentMiddleware"/>).</summary>
public static class AppClaimTypes
{
    /// <summary>String "true" or "false" — derived from Auth0 JWT <c>email_verified</c> in <see cref="Middleware.RoleEnrichmentMiddleware"/>.</summary>
    public const string EmailVerified = "app_email_verified";
}
