using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Odin.Api.Authentication;
using Odin.Api.Data.Entities;

namespace Odin.Api.Endpoints.Admin;

public static class HangfireSessionEndpoints
{
    /// <summary>
    /// Admin-only endpoint that mints an HttpOnly cookie scoped to <c>/jobs</c>. The SPA calls this
    /// (with the usual JWT bearer) right before opening the Hangfire dashboard in a new tab — top-level
    /// navigation can't carry the JWT, but it can carry the cookie. JWT remains the only way to reach
    /// every other API endpoint.
    /// </summary>
    public static void MapHangfireSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/admin/hangfire")
            .RequireAuthorization("AdminOnly");

        endpoints.MapPost("/session", StartHangfireSession)
            .Produces(StatusCodes.Status204NoContent)
            .WithSummary("Mint a short-lived cookie that lets the browser open the Hangfire dashboard.")
            .RequireRateLimiting("strict");
    }

    private static async Task<IResult> StartHangfireSession(
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Carry over the identity + admin claims so HangfireDashboardAuthFilter sees the same
        // app_role check the JWT path enforces. We deliberately project a small, fixed set of
        // claims rather than copying the entire JWT principal — the cookie should only authorize
        // the dashboard, not impersonate every API permission the bearer token had.
        var jwtUser = httpContext.User;
        var identityId = jwtUser.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? jwtUser.FindFirstValue("sub");

        if (string.IsNullOrEmpty(identityId))
        {
            return Results.Unauthorized();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, identityId),
            new("sub", identityId),
            new("app_role", AppRole.Admin.ToString()),
            new(AppClaimTypes.EmailVerified, "true"),
        };

        var identity = new ClaimsIdentity(claims, HangfireAuthScheme.Name);
        var principal = new ClaimsPrincipal(identity);

        await httpContext.SignInAsync(HangfireAuthScheme.Name, principal);
        return Results.NoContent();
    }
}
