using System.Security.Claims;
using Odin.Api.Endpoints.QpadmPopulationPanelSampleManagement.Models;

namespace Odin.Api.Endpoints.QpadmPopulationPanelSampleManagement;

/// <summary>
/// Scientist/Admin endpoints linking merge-panel samples (by stable <c>.ind</c> sample id) to
/// <c>QpadmPopulation</c>s. The samples are not DB rows — they live in the panel served by tools-api
/// (see the Panel Labels page) — so links are keyed by <c>(panel, sampleId)</c>.
/// </summary>
public static class QpadmPopulationPanelSampleEndpoints
{
    public static void MapQpadmPopulationPanelSampleEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/qpadm-population-panel-samples");

        endpoints.MapGet("/", GetLinks)
            .RequireAuthorization("ScientistOrAdmin")
            .RequireRateLimiting("authenticated")
            .Produces<IReadOnlyList<GetPanelSampleLinksContract.Response>>(StatusCodes.Status200OK);

        endpoints.MapPut("/sample", SetSamplePopulations)
            .RequireAuthorization("ScientistOrAdmin")
            .RequireRateLimiting("strict")
            .Produces<SetSamplePopulationsContract.Response>(StatusCodes.Status200OK);

        endpoints.MapPost("/bulk", BulkAssign)
            .RequireAuthorization("ScientistOrAdmin")
            .RequireRateLimiting("strict")
            .Produces<BulkAssignSamplePopulationsContract.Response>(StatusCodes.Status200OK);
    }

    private static async Task<IResult> GetLinks(IQpadmPopulationPanelSampleService service, string? panel)
    {
        if (string.IsNullOrWhiteSpace(panel)) return Results.BadRequest("Panel is required.");
        var list = await service.GetLinksAsync(panel);
        return Results.Ok(list);
    }

    private static async Task<IResult> SetSamplePopulations(
        HttpContext httpContext,
        IQpadmPopulationPanelSampleService service,
        SetSamplePopulationsContract.Request request)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Panel) || string.IsNullOrWhiteSpace(request.SampleId))
            return Results.BadRequest("Panel and SampleId are required.");

        var result = await service.SetSamplePopulationsAsync(identityId, request);
        return Results.Ok(result);
    }

    private static async Task<IResult> BulkAssign(
        HttpContext httpContext,
        IQpadmPopulationPanelSampleService service,
        BulkAssignSamplePopulationsContract.Request request)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Panel))
            return Results.BadRequest("Panel is required.");

        var result = await service.BulkAssignAsync(identityId, request);
        return Results.Ok(result);
    }

    private static string? ResolveIdentityId(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? httpContext.User.FindFirstValue("sub");
}
