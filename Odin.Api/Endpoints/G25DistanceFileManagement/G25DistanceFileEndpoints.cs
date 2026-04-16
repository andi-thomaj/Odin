using Odin.Api.Endpoints.G25DistanceFileManagement.Models;

namespace Odin.Api.Endpoints.G25DistanceFileManagement;

public static class G25DistanceFileEndpoints
{
    public static void MapG25DistanceFileEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/g25-distance-files");

        endpoints.MapGet("/", GetAll)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated");

        endpoints.MapGet("/{id:int}", GetById)
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

    private static async Task<IResult> GetAll(IG25DistanceFileService service)
    {
        var items = await service.GetAllAsync();
        return Results.Ok(items);
    }

    private static async Task<IResult> GetById(IG25DistanceFileService service, int id)
    {
        var item = await service.GetByIdAsync(id);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static async Task<IResult> Create(IG25DistanceFileService service, CreateG25DistanceFileContract.Request request)
    {
        var (response, error) = await service.CreateAsync(request);
        if (error is not null) return Results.BadRequest(error);
        return Results.Created($"/api/g25-distance-files/{response!.Id}", response);
    }

    private static async Task<IResult> Update(IG25DistanceFileService service, int id, UpdateG25DistanceFileContract.Request request)
    {
        var (response, error, notFound) = await service.UpdateAsync(id, request);
        if (notFound) return Results.NotFound();
        if (error is not null) return Results.BadRequest(error);
        return Results.Ok(response);
    }

    private static async Task<IResult> Delete(IG25DistanceFileService service, int id)
    {
        var ok = await service.DeleteAsync(id);
        return ok ? Results.NoContent() : Results.NotFound();
    }
}
