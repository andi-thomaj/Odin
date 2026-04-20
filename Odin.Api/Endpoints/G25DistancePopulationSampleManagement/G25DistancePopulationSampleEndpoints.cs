using System.Security.Claims;
using Odin.Api.Endpoints.G25DistancePopulationSampleManagement.Models;

namespace Odin.Api.Endpoints.G25DistancePopulationSampleManagement;

public static class G25DistancePopulationSampleEndpoints
{
    public static void MapG25DistancePopulationSampleEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/g25-distance-population-samples");

        endpoints.MapGet("/", GetPaged)
            .RequireAuthorization("ScientistOrAdmin")
            .RequireRateLimiting("authenticated");

        endpoints.MapGet("/all", GetAll)
            .RequireAuthorization("ScientistOrAdmin")
            .RequireRateLimiting("authenticated");

        endpoints.MapGet("/labels/search", SearchLabels)
            .RequireAuthorization("ScientistOrAdmin")
            .RequireRateLimiting("authenticated");

        endpoints.MapGet("/{id:int}", GetById)
            .RequireAuthorization("ScientistOrAdmin")
            .RequireRateLimiting("authenticated");

        endpoints.MapPost("/", Create)
            .RequireAuthorization("ScientistOrAdmin")
            .RequireRateLimiting("strict");

        endpoints.MapPut("/{id:int}", Update)
            .RequireAuthorization("ScientistOrAdmin")
            .RequireRateLimiting("strict");

        endpoints.MapDelete("/{id:int}", Delete)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict");
    }

    private static async Task<IResult> GetPaged(IG25DistancePopulationSampleService service, int page = 1, int pageSize = 25)
    {
        var result = await service.GetPagedAsync(page, pageSize);
        return Results.Ok(result);
    }

    private static async Task<IResult> GetAll(IG25DistancePopulationSampleService service)
    {
        var list = await service.GetAllAsync();
        return Results.Ok(list);
    }

    private static async Task<IResult> SearchLabels(IG25DistancePopulationSampleService service, string? q, int limit = 50)
    {
        var list = await service.SearchLabelsAsync(q ?? string.Empty, limit);
        return Results.Ok(list);
    }

    private static async Task<IResult> GetById(IG25DistancePopulationSampleService service, int id)
    {
        var row = await service.GetByIdAsync(id);
        return row is null ? Results.NotFound() : Results.Ok(row);
    }

    private static async Task<IResult> Create(HttpContext httpContext, IG25DistancePopulationSampleService service, CreateG25DistancePopulationSampleContract.Request request)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Label) || string.IsNullOrWhiteSpace(request.Coordinates))
            return Results.BadRequest("Label and Coordinates are required.");

        try
        {
            var result = await service.CreateAsync(identityId, request);
            return Results.Created($"/api/g25-distance-population-samples/{result.Id}", result);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> Update(
        HttpContext httpContext,
        IG25DistancePopulationSampleService service,
        int id,
        UpdateG25DistancePopulationSampleContract.Request request)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Label) || string.IsNullOrWhiteSpace(request.Coordinates))
            return Results.BadRequest("Label and Coordinates are required.");

        try
        {
            var result = await service.UpdateAsync(id, identityId, request);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(ex.Message);
        }
    }

    private static async Task<IResult> Delete(IG25DistancePopulationSampleService service, int id)
    {
        var ok = await service.DeleteAsync(id);
        return ok ? Results.NoContent() : Results.NotFound();
    }

    private static string? ResolveIdentityId(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? httpContext.User.FindFirstValue("sub");
}
