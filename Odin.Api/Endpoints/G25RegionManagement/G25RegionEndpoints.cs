using Odin.Api.Endpoints.G25RegionManagement.Models;

namespace Odin.Api.Endpoints.G25RegionManagement;

public static class G25RegionEndpoints
{
    public static void MapG25RegionEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/g25-regions");

        endpoints.MapGet("/", GetAll)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated");

        endpoints.MapGet("/{id:int}", GetById)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated");

        endpoints.MapGet("/by-ethnicity/{g25EthnicityId:int}", GetByEthnicityId)
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

    private static async Task<IResult> GetAll(IG25RegionService service)
    {
        var items = await service.GetAllAsync();
        return Results.Ok(items);
    }

    private static async Task<IResult> GetById(IG25RegionService service, int id)
    {
        var item = await service.GetByIdAsync(id);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static async Task<IResult> GetByEthnicityId(IG25RegionService service, int g25EthnicityId)
    {
        var items = await service.GetByEthnicityIdAsync(g25EthnicityId);
        return Results.Ok(items);
    }

    private static async Task<IResult> Create(IG25RegionService service, CreateG25RegionContract.Request request)
    {
        var (response, error) = await service.CreateAsync(request);
        if (error is not null) return Results.BadRequest(error);
        return Results.Created($"/api/g25-regions/{response!.Id}", response);
    }

    private static async Task<IResult> Update(IG25RegionService service, int id, UpdateG25RegionContract.Request request)
    {
        var (response, error, notFound) = await service.UpdateAsync(id, request);
        if (notFound) return Results.NotFound();
        if (error is not null) return Results.BadRequest(error);
        return Results.Ok(response);
    }

    private static async Task<IResult> Delete(IG25RegionService service, int id)
    {
        var (deleted, error, notFound) = await service.DeleteAsync(id);
        if (notFound) return Results.NotFound();
        if (error is not null) return Results.BadRequest(error);
        return deleted ? Results.NoContent() : Results.NotFound();
    }
}
