using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;

namespace Odin.Api.Middleware
{
    /// <summary>
    /// After JWT authentication succeeds, looks up the authenticated user in the database
    /// and adds their <c>app_role</c> claim to the current principal. This allows
    /// authorization policies to use database-managed roles.
    /// </summary>
    public class RoleEnrichmentMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context, ApplicationDbContext dbContext)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                var identityId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                                 ?? context.User.FindFirstValue("sub");

                if (!string.IsNullOrEmpty(identityId))
                {
                    var user = await dbContext.Users
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.IdentityId == identityId);

                    if (user is not null)
                    {
                        var roleClaim = new Claim("app_role", user.Role.ToString());
                        var appIdentity = new ClaimsIdentity([roleClaim], "AppRoleEnrichment");
                        context.User.AddIdentity(appIdentity);
                    }
                }
            }

            await next(context);
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
