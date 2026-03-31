using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;

namespace Odin.Api.Middleware
{
    /// <summary>
    /// After JWT authentication succeeds, looks up the authenticated user in the database
    /// and adds their <c>app_role</c> claim to the current principal. This allows
    /// authorization policies to use database-managed roles.
    /// Role is read from the database on every request (no in-memory cache) so promotions
    /// to Scientist/Admin take effect immediately; stale cached roles previously caused
    /// 403 on endpoints like qpAdm submission while the UI still showed the updated role.
    /// </summary>
    public class RoleEnrichmentMiddleware(
        RequestDelegate next,
        ILogger<RoleEnrichmentMiddleware> logger)
    {
        public async Task InvokeAsync(
            HttpContext context,
            ApplicationDbContext dbContext,
            IWebHostEnvironment environment)
        {
            if (environment.IsEnvironment("Testing"))
            {
                await next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true)
            {
                var identityId = ResolveIdentityId(context.User);

                if (!string.IsNullOrEmpty(identityId))
                {
                    var user = await FindUserByIdentityAsync(dbContext, identityId);

                    if (user is not null)
                    {
                        var roleClaim = new Claim("app_role", user.Role.ToString());
                        var appIdentity = new ClaimsIdentity([roleClaim], "AppRoleEnrichment");
                        context.User.AddIdentity(appIdentity);
                    }
                    else
                    {
                        LogNoUserRow(identityId);
                    }
                }
                else
                {
                    logger.LogWarning(
                        "Role enrichment: authenticated principal has no sub/nameidentifier claim; cannot load app_role from application_users.");
                }
            }

            await next(context);
        }

        /// <summary>
        /// Access token subject must match <see cref="User.IdentityId"/>.
        /// Prefer mapped NameIdentifier, then raw <c>sub</c>, then namespaced <c>*/sub</c> claims.
        /// </summary>
        private static string? ResolveIdentityId(ClaimsPrincipal principal)
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

        private async Task<User?> FindUserByIdentityAsync(
            ApplicationDbContext dbContext,
            string identityId)
        {
            var user = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdentityId == identityId);

            if (user is not null)
                return user;

            // Manual DB edits sometimes differ in casing from Auth0's sub; policies require exact app_role.
            user = await dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.IdentityId.ToLower() == identityId.ToLower());

            if (user is not null)
            {
                logger.LogWarning(
                    "Role enrichment: matched application_users by case-insensitive identity_id. " +
                    "Update identity_id to match the access token sub exactly to avoid ambiguity.");
            }

            return user;
        }

        private void LogNoUserRow(string identityId)
        {
            var preview = identityId.Length > 16 ? identityId[..16] + "…" : identityId;
            logger.LogWarning(
                "Role enrichment: no application_users row for token identity (prefix: {IdentityPrefix}). " +
                "Policies that require app_role will return 403. " +
                "Set identity_id to the same value as the Auth0 access token \"sub\" claim (decode JWT or Auth0 Dashboard → Test).",
                preview);
        }
    }

    public static class RoleEnrichmentMiddlewareExtensions
    {
        public static IApplicationBuilder UseRoleEnrichment(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<RoleEnrichmentMiddleware>();
        }
    }
}
