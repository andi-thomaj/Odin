namespace Odin.Api.Authentication;

/// <summary>
/// Per-request application context — the <c>applications.key</c> resolved from the <c>X-App</c> request header
/// by <see cref="Middleware.AppResolutionMiddleware"/>. Consumed by the auth hot path
/// (<see cref="Services.IUserProvisioningService"/>, <see cref="Middleware.RoleEnrichmentMiddleware"/>) and by
/// <see cref="Data.ApplicationDbContext"/> for app-scoped query filters + write stamping. Defaults to
/// <see cref="AppKeys.Ancestrify"/> until set, so background work that runs with no HTTP request resolves to
/// the original app rather than throwing.
/// </summary>
public interface IAppContext
{
    /// <summary>The resolved app key for the current request (or <see cref="AppKeys.Ancestrify"/> outside a request).</summary>
    string App { get; }
}

/// <summary>
/// Mutable, request-scoped <see cref="IAppContext"/>. Named <c>RequestAppContext</c> to avoid colliding with
/// the BCL <see cref="System.AppContext"/>. <see cref="Middleware.AppResolutionMiddleware"/> resolves this
/// concrete type once per request and calls <see cref="SetApp"/>; everyone else depends on the read-only
/// <see cref="IAppContext"/> (the same scoped instance is forwarded — see <c>Program.cs</c> registration).
/// </summary>
public sealed class RequestAppContext : IAppContext
{
    public string App { get; private set; } = AppKeys.Ancestrify;

    /// <summary>Sets the resolved app for this request. Called once, early, by the app-resolution middleware.</summary>
    public void SetApp(string app)
    {
        if (!string.IsNullOrWhiteSpace(app))
            App = app;
    }
}
