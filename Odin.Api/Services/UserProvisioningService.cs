using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Odin.Api.Authentication;
using Odin.Api.Data;
using Odin.Api.Data.Entities;

namespace Odin.Api.Services;

public interface IUserProvisioningService
{
    /// <summary>
    /// Returns the <see cref="User"/> row that backs the authenticated principal, inserting one if it
    /// does not yet exist. Returns <c>null</c> when the principal has no <c>sub</c>/<c>nameidentifier</c>
    /// claim, when Auth0 reports the email as unverified, or when no email can be resolved from JWT
    /// claims or <c>/userinfo</c>. Safe to call on every authenticated request — does nothing once the
    /// row exists.
    /// </summary>
    Task<User?> EnsureUserAsync(
        ClaimsPrincipal principal,
        string? accessToken,
        string? ipAddress,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Recreates the <c>application_users</c> row for an Auth0-authenticated principal when the row is
/// missing — covers the case where a user was deleted from the DB but still has a valid Auth0 session,
/// and the case where backend requests race the front-end <c>useAuthSync</c> registration hook.
/// </summary>
public class UserProvisioningService(
    ApplicationDbContext dbContext,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IMemoryCache memoryCache,
    IGeoLocationService geoLocationService,
    IAppContext appContext,
    ILogger<UserProvisioningService> logger) : IUserProvisioningService
{
    public async Task<User?> EnsureUserAsync(
        ClaimsPrincipal principal,
        string? accessToken,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var identityId = principal.GetIdentityId();
        if (string.IsNullOrWhiteSpace(identityId))
            return null;

        var existing = await FindAsync(identityId, cancellationToken);
        if (existing is not null)
            return existing;

        var profile = await ResolveProfileAsync(principal, accessToken, cancellationToken);

        if (profile.EmailVerified == false)
        {
            LogIdentity("JIT user provisioning skipped: Auth0 reports email as unverified.", identityId);
            return null;
        }

        if (string.IsNullOrWhiteSpace(profile.Email))
        {
            LogIdentity(
                "JIT user provisioning skipped: no email available on JWT claims or Auth0 /userinfo.",
                identityId);
            return null;
        }

        var (firstName, lastName) = SplitName(profile.GivenName, profile.FamilyName, profile.Name);
        var geo = await geoLocationService.GetCountryFromIpAsync(ipAddress);
        var now = DateTime.UtcNow;

        var user = new User
        {
            IdentityId = identityId,
            App = appContext.App,
            Email = profile.Email!,
            Username = profile.Nickname ?? profile.Email!,
            FirstName = firstName ?? string.Empty,
            MiddleName = string.Empty,
            LastName = lastName ?? string.Empty,
            Role = AppRole.User,
            Country = geo?.Country,
            CountryCode = geo?.CountryCode,
            CreatedAt = now,
            CreatedBy = identityId,
            UpdatedAt = now,
            UpdatedBy = identityId,
        };

        dbContext.Users.Add(user);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            LogIdentity(
                $"JIT-provisioned application_users row (email={profile.Email}).",
                identityId,
                LogLevel.Information);
            return user;
        }
        catch (DbUpdateException ex)
        {
            // A parallel request already inserted the row; detach our copy and reload.
            logger.LogWarning(ex, "JIT user provisioning insert collided; reloading existing row.");
            dbContext.Entry(user).State = EntityState.Detached;
            return await FindAsync(identityId, cancellationToken);
        }
    }

    private async Task<User?> FindAsync(string identityId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.IdentityId == identityId, cancellationToken);
        if (user is not null)
            return user;

        // Mirrors RoleEnrichmentMiddleware's case-insensitive fallback for hand-edited identity_id values.
        return await dbContext.Users
            .FirstOrDefaultAsync(u => u.IdentityId.ToLower() == identityId.ToLower(), cancellationToken);
    }

    private async Task<Auth0UserInfoProfile> ResolveProfileAsync(
        ClaimsPrincipal principal,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        var fromJwt = ReadProfileFromJwt(principal);

        if (HasUsableProfile(fromJwt) || string.IsNullOrWhiteSpace(accessToken))
            return fromJwt;

        var authority = configuration["Jwt:Authority"];
        if (string.IsNullOrWhiteSpace(authority))
            return fromJwt;

        try
        {
            var cacheKey = "od_uinfo_profile_v1:" +
                           Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(accessToken)));

            var fetched = await memoryCache.GetOrCreateAsync(cacheKey, async entry =>
            {
                entry.SetAbsoluteExpiration(TimeSpan.FromMinutes(15));
                var client = httpClientFactory.CreateClient("Auth0UserInfo");
                var (profile, _) = await Auth0RetryPolicy.ExecuteAsync(
                    ct => Auth0UserInfoClient.GetAsync(client, authority!, accessToken!, ct),
                    logger,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                return profile;
            }).ConfigureAwait(false);

            if (fetched is null)
                return fromJwt;

            return new Auth0UserInfoProfile(
                Email: fromJwt.Email ?? fetched.Email,
                EmailVerified: fromJwt.EmailVerified ?? fetched.EmailVerified,
                Name: fromJwt.Name ?? fetched.Name,
                GivenName: fromJwt.GivenName ?? fetched.GivenName,
                FamilyName: fromJwt.FamilyName ?? fetched.FamilyName,
                Nickname: fromJwt.Nickname ?? fetched.Nickname);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "JIT user provisioning: Auth0 /userinfo lookup failed.");
            return fromJwt;
        }
    }

    private static Auth0UserInfoProfile ReadProfileFromJwt(ClaimsPrincipal principal)
    {
        var email = principal.FindFirst("email")?.Value
                    ?? principal.FindFirst(ClaimTypes.Email)?.Value;
        var emailVerified = Auth0EmailVerifiedClaims.GetJwtEmailVerifiedBoolean(principal);
        return new Auth0UserInfoProfile(
            Email: string.IsNullOrWhiteSpace(email) ? null : email,
            EmailVerified: emailVerified,
            Name: principal.FindFirst("name")?.Value,
            GivenName: principal.FindFirst("given_name")?.Value,
            FamilyName: principal.FindFirst("family_name")?.Value,
            Nickname: principal.FindFirst("nickname")?.Value);
    }

    private static bool HasUsableProfile(Auth0UserInfoProfile profile) =>
        !string.IsNullOrWhiteSpace(profile.Email) && profile.EmailVerified.HasValue;

    private static (string? FirstName, string? LastName) SplitName(string? given, string? family, string? full)
    {
        if (!string.IsNullOrWhiteSpace(given) || !string.IsNullOrWhiteSpace(family))
            return (given, family);
        if (string.IsNullOrWhiteSpace(full))
            return (null, null);
        var parts = full.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (null, null),
            1 => (parts[0], null),
            _ => (parts[0], parts[1]),
        };
    }

    private void LogIdentity(string message, string identityId, LogLevel level = LogLevel.Warning)
    {
        var preview = identityId.Length > 16 ? identityId[..16] + "…" : identityId;
        logger.Log(level, "{Message} Identity prefix: {IdentityPrefix}", message, preview);
    }
}
