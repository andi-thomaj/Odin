namespace Odin.Api.Configuration;

/// <summary>
/// Apple In-App Purchase (StoreKit 2) validation settings for the iOS app. The backend cryptographically
/// verifies the signed transaction JWS — and that it is for the right bundle id, product, and service —
/// before creating a paid qpAdm/G25 order. Secrets/paths come from appsettings / user-secrets (dev) or
/// Coolify env (prod), bound in <c>Program.cs</c>.
/// </summary>
public sealed class AppleIapOptions
{
    public const string SectionName = "AppleIap";

    /// <summary>Expected app bundle id inside the signed transaction.</summary>
    public string BundleId { get; set; } = "io.ancestrify.app";

    /// <summary>App Store product id for the qpAdm consumable.</summary>
    public string QpadmProductId { get; set; } = "io.ancestrify.app.qpadm";

    /// <summary>App Store product id for the G25 consumable.</summary>
    public string G25ProductId { get; set; } = "io.ancestrify.app.g25";

    /// <summary>App Store product id for the "Through the Ages" AI ancestral-portraits add-on (consumable, one unlock per order).</summary>
    public string AiPortraitsProductId { get; set; } = "io.ancestrify.app.aiportraits";

    /// <summary>App Store product id for the Y-DNA results unlock (consumable, one unlock per order — $9.99).</summary>
    public string YDnaProductId { get; set; } = "io.ancestrify.app.ydna";

    /// <summary>
    /// Authoritative price stamped on a created qpAdm order. Informational only — Apple charges the
    /// buyer's storefront price tier, which varies by region; this is what we record/display server-side.
    /// </summary>
    public decimal QpadmPrice { get; set; } = 49.90m;

    /// <summary>Authoritative price stamped on a created G25 order (see <see cref="QpadmPrice"/>).</summary>
    public decimal G25Price { get; set; } = 39.90m;

    /// <summary>
    /// When true (the production default), the transaction's signature and Apple certificate chain are
    /// cryptographically verified against <see cref="AppleRootCertPath"/>. Set false ONLY for local dev or
    /// Xcode StoreKit-testing, where transactions are signed by a local test certificate, not Apple's root.
    /// Signature verification is also always skipped under the <c>Testing</c> host environment so the
    /// integration suite can craft transactions without Apple's private key.
    /// </summary>
    public bool VerifySignature { get; set; } = true;

    /// <summary>
    /// Optional filesystem path to an Apple Root CA - G3 certificate (DER <c>AppleRootCA-G3.cer</c>) used to
    /// anchor the chain. When empty (the default), the API uses the <c>AppleRootCA-G3.cer</c> bundled with it
    /// (embedded resource), so no file needs deploying. Set this only to override with a rotated root without
    /// a rebuild.
    /// </summary>
    public string? AppleRootCertPath { get; set; }

    /// <summary>
    /// Accepted values of the transaction payload's <c>environment</c>. Production should allow
    /// <c>Production</c> (and usually <c>Sandbox</c> for App Review). Dev/test can add <c>Xcode</c>.
    /// </summary>
    public string[] AllowedEnvironments { get; set; } = ["Production", "Sandbox"];
}
