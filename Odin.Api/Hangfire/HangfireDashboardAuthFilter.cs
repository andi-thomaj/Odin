using Hangfire.Dashboard;
using Odin.Api.Data.Entities;

namespace Odin.Api.Hangfire;

/// <summary>
/// Restricts Hangfire dashboard access to authenticated users with the Admin role.
/// Hangfire's dashboard bypasses the normal ASP.NET authorization pipeline, so the
/// role check has to be re-asserted via this filter.
/// </summary>
internal sealed class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        var user = httpContext.User;

        if (user.Identity?.IsAuthenticated != true) return false;

        // Matches the AdminOnly policy: app_role claim must equal Admin.
        return user.HasClaim("app_role", AppRole.Admin.ToString());
    }
}
