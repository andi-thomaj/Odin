namespace Odin.Api.Endpoints.AuthRegistration;

/// <summary>
/// Auth0 Database connection settings for <c>/dbconnections/signup</c>.
/// Dashboard: enable Database connection, allow sign-ups, attach the SPA application to the connection.
/// </summary>
public sealed class Auth0SignupOptions
{
    public const string SectionName = "Auth0";

    /// <summary>Auth0 tenant hostname (no scheme), e.g. <c>dev-xxx.us.auth0.com</c>.</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>Same as the SPA <c>VITE_AUTH0_CLIENT_ID</c> — public client allowed to call signup.</summary>
    public string SpaClientId { get; set; } = string.Empty;

    /// <summary>Connection name, usually <c>Username-Password-Authentication</c>.</summary>
    public string DatabaseConnection { get; set; } = "Username-Password-Authentication";
}
