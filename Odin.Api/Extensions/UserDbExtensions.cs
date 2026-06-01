using Microsoft.EntityFrameworkCore;
using Odin.Api.Data.Entities;

namespace Odin.Api.Extensions;

/// <summary>
/// Shared lookups for the authenticated user row. The "load by Auth0 sub or throw" pattern was
/// duplicated across services with subtly different error messages; centralising it here keeps
/// the contract consistent (and the message recognisable in error reports).
/// </summary>
public static class UserDbExtensions
{
    /// <summary>
    /// Loads the <c>application_users</c> row for <paramref name="identityId"/> or throws
    /// <see cref="InvalidOperationException"/> with the canonical message. <c>RoleEnrichmentMiddleware</c>
    /// JIT-provisions the row on the first authenticated request, so by the time a service handler
    /// runs the row should exist; a missing row means JIT provisioning failed (verification, DB) and
    /// the caller should surface a 400.
    /// </summary>
    public static async Task<User> RequireByIdentityAsync(
        this IQueryable<User> users,
        string identityId,
        CancellationToken cancellationToken = default)
    {
        var user = await users.FirstOrDefaultAsync(u => u.IdentityId == identityId, cancellationToken);
        return user ?? throw new InvalidOperationException("Authenticated user not found in the database.");
    }
}
