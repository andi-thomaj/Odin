using Odin.Api.Endpoints.G25EraManagement.Models;

namespace Odin.Api.Endpoints.G25EraManagement;

public static class G25EraEndpoints
{
    public static void MapG25EraEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/g25-eras");

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

    private static async Task<IResult> GetAll(IG25EraService service)
    {
        var items = await service.GetAllAsync();
        return Results.Ok(items);
    }

    private static async Task<IResult> GetById(IG25EraService service, int id)
    {
        var item = await service.GetByIdAsync(id);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static async Task<IResult> Create(IG25EraService service, CreateG25EraContract.Request request)
    {
        var (response, error) = await service.CreateAsync(request);
        if (error is not null) return Results.BadRequest(error);
        return Results.Created($"/api/g25-eras/{response!.Id}", response);
    }

    private static async Task<IResult> Update(IG25EraService service, int id, UpdateG25EraContract.Request request)
    {
        var (response, error, notFound) = await service.UpdateAsync(id, request);
        if (notFound) return Results.NotFound();
        if (error is not null) return Results.BadRequest(error);
        return Results.Ok(response);
    }

    private static async Task<IResult> Delete(IG25EraService service, int id)
    {
        var ok = await service.DeleteAsync(id);
        return ok ? Results.NoContent() : Results.NotFound();
    }
}
