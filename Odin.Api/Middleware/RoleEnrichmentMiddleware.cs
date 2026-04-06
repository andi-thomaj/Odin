using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Odin.Api.Authentication;
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
    /// <see cref="AppClaimTypes.EmailVerified"/> is derived from the Auth0 JWT <c>email_verified</c> when present;
    /// otherwise from Auth0 <c>/userinfo</c> using the saved access token (Auth0 API tokens often omit the claim).
    /// Userinfo is cached per access token to avoid parallel dashboard requests triggering Auth0 rate limits (HTTP 429).
    /// </summary>
    public class RoleEnrichmentMiddleware(
        RequestDelegate next,
        ILogger<RoleEnrichmentMiddleware> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IMemoryCache memoryCache)
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
                var identityId = context.User.GetIdentityId();

                if (!string.IsNullOrEmpty(identityId))
                {
                    var emailVerified = await ResolveAppEmailVerifiedAsync(context);

                    var user = await FindUserByIdentityAsync(dbContext, identityId);

                    if (user is not null)
                    {
                        var roleClaim = new Claim("app_role", user.Role.ToString());
                        var emailVerifiedClaim = new Claim(AppClaimTypes.EmailVerified, emailVerified);
                        var appIdentity = new ClaimsIdentity([roleClaim, emailVerifiedClaim], "AppRoleEnrichment");
                        context.User.AddIdentity(appIdentity);
                    }
                    else
                    {
                        LogNoUserRow(identityId);
                        var emailOnly = new ClaimsIdentity(
                            [new Claim(AppClaimTypes.EmailVerified, emailVerified)],
                            "AppEmailEnrichment");
                        context.User.AddIdentity(emailOnly);
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

        private async Task<string> ResolveAppEmailVerifiedAsync(HttpContext context)
        {
            var fromJwt = Auth0EmailVerifiedClaims.GetJwtEmailVerifiedBoolean(context.User);
            if (fromJwt.HasValue)
                return fromJwt.Value ? "true" : "false";

            var (accessToken, _) = await TryGetRawAccessTokenWithSourceAsync(context).ConfigureAwait(false);
            if (string.IsNullOrEmpty(accessToken))
            {
                logger.LogWarning(
                    "Role enrichment: JWT has no email_verified claim and no bearer access token was available " +
                    "(SaveToken + GetTokenAsync or Authorization header); treating as unverified.");
                return "false";
            }

            var authority = configuration["Jwt:Authority"];
            if (string.IsNullOrWhiteSpace(authority))
                return "false";

            try
            {
                var cacheKey = "od_ev_v1:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accessToken)));

                try
                {
                    var verified = await memoryCache
                        .GetOrCreateAsync(
                            cacheKey,
                            async entry =>
                            {
                                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(15));

                                var client = httpClientFactory.CreateClient("Auth0UserInfo");
                                string v = "false";
                                int status = 0;
                                for (var attempt = 0; attempt < 3; attempt++)
                                {
                                    var (v2, s2) = await Auth0UserInfoEmailVerified.GetAppEmailVerifiedWithStatusAsync(
                                        client,
                                        authority,
                                        accessToken,
                                        context.RequestAborted).ConfigureAwait(false);
                                    v = v2;
                                    status = s2;
                                    if (s2 == 200)
                                        break;
                                    if (s2 == 429 && attempt < 2)
                                    {
                                        await Task.Delay(100 * (attempt + 1), context.RequestAborted).ConfigureAwait(false);
                                        continue;
                                    }

                                    break;
                                }

                                if (status != 200)
                                {
                                    logger.LogWarning(
                                        "Role enrichment: Auth0 userinfo returned {StatusCode}; email verification fallback failed. " +
                                        "Ensure the SPA requests openid profile email scopes and/or add an Auth0 Action to put email_verified on the API access token.",
                                        status);
                                    throw new InvalidOperationException($"userinfo_http_{status}");
                                }

                                return v;
                            })
                        .ConfigureAwait(false);

                    return verified ?? "false";
                }
                catch (InvalidOperationException ex) when (ex.Message.StartsWith("userinfo_http_", StringComparison.Ordinal))
                {
                    return "false";
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Role enrichment: Auth0 userinfo fallback failed; treating as unverified.");
                return "false";
            }
        }

        /// <summary>
        /// Prefer the token saved by JWT bearer (<see cref="JwtBearerOptions.SaveToken"/>); must use the Bearer scheme name.
        /// Fall back to the Authorization header (covers edge cases and matches what the API validates).
        /// </summary>
        private static async Task<(string? Token, string Source)> TryGetRawAccessTokenWithSourceAsync(HttpContext context)
        {
            var fromStore = await context
                .GetTokenAsync(JwtBearerDefaults.AuthenticationScheme, "access_token")
                .ConfigureAwait(false);
            if (!string.IsNullOrEmpty(fromStore))
                return (fromStore, "saved_access_token");

            var authHeader = context.Request.Headers.Authorization.ToString();
            if (string.IsNullOrEmpty(authHeader))
                return (null, "none_no_header");

            const string prefix = "Bearer ";
            if (!authHeader.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return (null, "none_bad_scheme");

            return (authHeader[prefix.Length..].Trim(), "authorization_header");
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
