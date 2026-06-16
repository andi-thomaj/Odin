namespace Odin.Api.Authentication;

/// <summary>
/// Canonical application keys. The <c>applications</c> table is the source of truth for which apps exist, but
/// <see cref="Ancestrify"/> is anchored in code because it is the original application (odin-react): it is the
/// default when no <c>X-App</c> header is present (legacy callers, server-internal/background work) and the
/// backfill target for every pre-existing row. New apps are added as <c>applications</c> rows, not constants.
/// </summary>
public static class AppKeys
{
    /// <summary>The original application (odin-react / Ancestrify). Default app + migration backfill target.</summary>
    public const string Ancestrify = "ancestrify";
}
