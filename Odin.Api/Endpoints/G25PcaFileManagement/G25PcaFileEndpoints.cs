using Odin.Api.Endpoints.G25PcaFileManagement.Models;

namespace Odin.Api.Endpoints.G25PcaFileManagement;

public static class G25PcaFileEndpoints
{
    public static void MapG25PcaFileEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/g25-pca-files");

        endpoints.MapGet("/", GetAll)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated");

        endpoints.MapGet("/{id:int}", GetById)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("authenticated");

        endpoints.MapGet("/by-era/{g25DistanceEraId:int}", GetByEraId)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("authenticated");

        endpoints.MapGet("/by-continents", GetByContinents)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated");

        endpoints.MapPost("/", Create)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict");

        endpoints.MapPut("/{id:int}", Update)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict");

        endpoints.MapDelete("/{id:int}", Delete)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict");
    }

    private static async Task<IResult> GetAll(IG25PcaFileService service)
    {
        var items = await service.GetAllAsync();
        return Results.Ok(items);
    }

    private static async Task<IResult> GetById(IG25PcaFileService service, int id)
    {
        var item = await service.GetByIdAsync(id);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static async Task<IResult> GetByEraId(IG25PcaFileService service, int g25DistanceEraId)
    {
        var item = await service.GetByEraIdAsync(g25DistanceEraId);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static async Task<IResult> GetByContinents(
        IG25PcaFileService service,
        [Microsoft.AspNetCore.Mvc.FromQuery(Name = "ids")] int[] ids,
        CancellationToken ct)
    {
        var (response, error, notFound) = await service.GetByContinentIdsAsync(ids ?? [], ct);
        if (notFound) return Results.NotFound(error);
        if (error is not null) return Results.BadRequest(error);
        return Results.Ok(response);
    }

    private static async Task<IResult> Create(IG25PcaFileService service, CreateG25PcaFileContract.Request request)
    {
        var (response, error) = await service.CreateAsync(request);
        if (error is not null) return Results.BadRequest(error);
        return Results.Created($"/api/g25-pca-files/{response!.Id}", response);
    }

    private static async Task<IResult> Update(IG25PcaFileService service, int id, UpdateG25PcaFileContract.Request request)
    {
        var (response, error, notFound) = await service.UpdateAsync(id, request);
        if (notFound) return Results.NotFound();
        if (error is not null) return Results.BadRequest(error);
        return Results.Ok(response);
    }

    private static async Task<IResult> Delete(IG25PcaFileService service, int id)
    {
        var ok = await service.DeleteAsync(id);
        return ok ? Results.NoContent() : Results.NotFound();
    }
}
