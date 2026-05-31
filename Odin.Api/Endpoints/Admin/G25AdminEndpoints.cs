using System.Security.Claims;
using Hangfire;
using Odin.Api.Endpoints.Admin.Models;
using Odin.Api.Endpoints.OrderManagement;

namespace Odin.Api.Endpoints.Admin;

public static class G25AdminEndpoints
{
    public static void MapG25AdminEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/admin/g25")
            .RequireAuthorization("AdminOnly");

        endpoints.MapGet("/inspections", GetInspections)
            .Produces<List<AdminG25InspectionContract.ListItem>>(StatusCodes.Status200OK);

        endpoints.MapPost("/recompute-distance-results", RecomputeDistanceResults)
            .RequireRateLimiting("strict");

        endpoints.MapPost("/import-distance-population-samples", ImportDistancePopulationSamples)
            .RequireRateLimiting("strict")
            .Produces<ImportG25DistancePopulationSamplesContract.Response>(StatusCodes.Status200OK)
            .WithRequestTimeout(TimeSpan.FromMinutes(5));
    }

    private static async Task<IResult> GetInspections(IOrderService service)
    {
        var items = await service.GetAdminG25InspectionsAsync();
        return Results.Ok(items);
    }

    private static IResult RecomputeDistanceResults(
        RecomputeG25DistancesContract.Request? request,
        IBackgroundJobClient jobClient,
        HttpContext httpContext)
    {
        var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub")
                         ?? string.Empty;

        // Hangfire materializes the IOrderService inside its own DI scope when the worker picks
        // up the job; this avoids blocking the request thread on the multi-minute compute.
        var inspectionIds = request?.InspectionIds?.ToList();
        var jobId = jobClient.Enqueue<IOrderService>(svc =>
            svc.RecomputeG25DistanceResultsAsync(identityId, inspectionIds));

        return Results.Accepted(value: new { jobId });
    }

    private static async Task<IResult> ImportDistancePopulationSamples(
        IG25SeedImportService importService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub")
                         ?? string.Empty;

        var result = await importService.ImportDistancePopulationSamplesAsync(identityId, cancellationToken);
        return Results.Ok(result);
    }
}
