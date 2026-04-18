using System.Security.Claims;
using Odin.Api.Endpoints.Admin.Models;
using Odin.Api.Endpoints.OrderManagement;

namespace Odin.Api.Endpoints.Admin;

public static class G25AdminEndpoints
{
    public static void MapG25AdminEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/admin/g25")
            .RequireAuthorization("AdminOnly");

        endpoints.MapGet("/inspections", GetInspections);

        endpoints.MapPost("/recompute-distance-results", RecomputeDistanceResults)
            .RequireRateLimiting("strict")
            .WithRequestTimeout(TimeSpan.FromMinutes(10));
    }

    private static async Task<IResult> GetInspections(IOrderService service)
    {
        var items = await service.GetAdminG25InspectionsAsync();
        return Results.Ok(items);
    }

    private static async Task<IResult> RecomputeDistanceResults(
        RecomputeG25DistancesContract.Request? request,
        IOrderService service,
        HttpContext httpContext)
    {
        var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub")
                         ?? string.Empty;

        var response = await service.RecomputeG25DistanceResultsAsync(identityId, request?.InspectionIds);
        return Results.Ok(response);
    }
}
