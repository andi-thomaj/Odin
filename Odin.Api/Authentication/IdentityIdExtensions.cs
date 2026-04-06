using System.Security.Claims;

namespace Odin.Api.Authentication;

public static class IdentityIdExtensions
{
    /// <summary>Matches <see cref="Middleware.RoleEnrichmentMiddleware"/> resolution for Auth0 JWT <c>sub</c>.</summary>
    public static string? GetIdentityId(this ClaimsPrincipal principal)
    {
        var id = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrWhiteSpace(id))
            return id.Trim();

        id = principal.FindFirstValue("sub");
        if (!string.IsNullOrWhiteSpace(id))
            return id.Trim();

        foreach (var claim in principal.Claims)
        {
            if (claim.Type.EndsWith("/sub", StringComparison.Ordinal))
                return claim.Value.Trim();
        }

        return null;
    }
}
