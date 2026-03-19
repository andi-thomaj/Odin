using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Odin.Api.Data;

namespace Odin.Api.Middleware
{
    /// <summary>
    /// After JWT authentication succeeds, looks up the authenticated user in the database
    /// and adds their <c>app_role</c> claim to the current principal. This allows
    /// authorization policies to use database-managed roles.
    /// Results are cached per identity for 5 minutes to avoid a DB query on every request.
    /// </summary>
    public class RoleEnrichmentMiddleware(RequestDelegate next)
    {
        internal const string CacheKeyPrefix = "UserRole_";

        public async Task InvokeAsync(
            HttpContext context,
            ApplicationDbContext dbContext,
            IMemoryCache cache,
            IWebHostEnvironment environment)
        {
            if (environment.IsEnvironment("Testing"))
            {
                await next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated == true)
            {
                var identityId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                                 ?? context.User.FindFirstValue("sub");

                if (!string.IsNullOrEmpty(identityId))
                {
                    var cacheKey = CacheKeyPrefix + identityId;

                    if (!cache.TryGetValue(cacheKey, out string? cachedRole))
                    {
                        var user = await dbContext.Users
                            .AsNoTracking()
                            .FirstOrDefaultAsync(u => u.IdentityId == identityId);

                        if (user is not null)
                        {
                            cachedRole = user.Role.ToString();
                            cache.Set(cacheKey, cachedRole, new MemoryCacheEntryOptions
                            {
                                SlidingExpiration = TimeSpan.FromMinutes(5)
                            });
                        }
                    }

                    if (cachedRole is not null)
                    {
                        var roleClaim = new Claim("app_role", cachedRole);
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
