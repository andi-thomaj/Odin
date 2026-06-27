namespace Odin.Api.Authentication;

/// <summary>
/// Cache keys for the per-request role lookup performed by
/// <see cref="Odin.Api.Middleware.RoleEnrichmentMiddleware"/>. The lookup hit the database on every
/// authenticated request; it is now cached per Auth0 identity with a short TTL safety net and
/// <b>invalidated on every write</b> to <c>User.Role</c> (role update, user delete) so promotions to
/// Scientist/Admin still take effect immediately — preserving the original no-stale-role guarantee.
/// </summary>
public static class UserRoleCacheKeys
{
    /// <summary>Short safety-net TTL; writes invalidate explicitly, so this only bounds drift from out-of-band DB edits.</summary>
    public static readonly TimeSpan Ttl = TimeSpan.FromMinutes(5);

    public static string ForIdentity(string identityId) => "od_user_role_v1:" + identityId;
}
