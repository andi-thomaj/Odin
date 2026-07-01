namespace Odin.Api.Storage;

/// <summary>
/// R2 key conventions for PER-USER private data. Face photos (<c>users/{slug}/face-photos/…</c>) and ancestral
/// portraits (<c>users/{slug}/ancestral-portraits/…</c>) both live under <c>users/{slug}/</c>, where the slug is the
/// user's <c>IdentityId</c> sanitised to a key-safe form. Centralised so the upload paths AND the orphan-cleanup
/// sweep compute the SAME slug — the sweep deletes any <c>users/{slug}/</c> whose slug matches no current DB user, so
/// it must derive the slug identically or it could miss (or, worse, wrongly target) a user's data.
/// </summary>
public static class UserStorageKeys
{
    /// <summary>The root prefix under which ALL per-user private R2 data lives.</summary>
    public const string RootPrefix = "users/";

    /// <summary><c>IdentityId</c> → key-safe slug (letters/digits/<c>-</c>/<c>_</c> kept, everything else → <c>_</c>).</summary>
    public static string Sanitize(string identityId) =>
        new(identityId.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());

    /// <summary>The R2 prefix for one user's data, e.g. <c>users/google-oauth2_123/</c>.</summary>
    public static string UserPrefix(string identityId) => $"{RootPrefix}{Sanitize(identityId)}/";
}
