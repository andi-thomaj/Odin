using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Odin.Api.Data.Entities;

namespace Odin.Api.Authentication;

/// <summary>
/// Test-only authentication scheme (environment "Testing"). Claims come from headers so integration tests
/// can satisfy authorization policies without JWT.
/// </summary>
public sealed class TestAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (Request.Headers.TryGetValue("X-Test-Unauthenticated", out var unauth)
            && string.Equals(unauth.ToString(), "true", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(AuthenticateResult.NoResult());

        var identityId = Request.Headers["X-Test-Identity-Id"].FirstOrDefault() ?? "auth0|integration-default";
        var roleHeader = Request.Headers["X-Test-App-Role"].FirstOrDefault();
        var role = Enum.TryParse<AppRole>(roleHeader, ignoreCase: true, out var parsed)
            ? parsed.ToString()
            : AppRole.User.ToString();

        var emailVerifiedHeader = Request.Headers["X-Test-Email-Verified"].FirstOrDefault();
        var emailVerified = string.Equals(emailVerifiedHeader, "false", StringComparison.OrdinalIgnoreCase)
            ? "false"
            : "true";

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identityId),
            new("sub", identityId),
            new(ClaimTypes.Name, identityId),
            new("app_role", role),
            new(AppClaimTypes.EmailVerified, emailVerified)
        };

        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        return Task.FromResult(
            AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
