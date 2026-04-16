using Odin.Api.Endpoints.G25EthnicityManagement.Models;

namespace Odin.Api.Endpoints.G25EthnicityManagement;

public static class G25EthnicityEndpoints
{
    public static void MapG25EthnicityEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/g25-ethnicities");

        endpoints.MapGet("/", GetAll)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated");

        endpoints.MapGet("/admin", GetAllAdmin)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("authenticated");

        endpoints.MapGet("/admin/{id:int}", GetByIdAdmin)
            .RequireAuthorization("AdminOnly")
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

    private static async Task<IResult> GetAll(IG25EthnicityService service)
    {
        var items = await service.GetAllAsync();
        return Results.Ok(items);
    }

    private static async Task<IResult> GetAllAdmin(IG25EthnicityService service)
    {
        var items = await service.GetAllAdminAsync();
        return Results.Ok(items);
    }

    private static async Task<IResult> GetByIdAdmin(IG25EthnicityService service, int id)
    {
        var item = await service.GetByIdAdminAsync(id);
        return item is null ? Results.NotFound() : Results.Ok(item);
    }

    private static async Task<IResult> Create(IG25EthnicityService service, CreateG25EthnicityContract.Request request)
    {
        var (response, error) = await service.CreateAsync(request);
        if (error is not null) return Results.BadRequest(error);
        return Results.Created($"/api/g25-ethnicities/admin/{response!.Id}", response);
    }

    private static async Task<IResult> Update(IG25EthnicityService service, int id, UpdateG25EthnicityContract.Request request)
    {
        var (response, error, notFound) = await service.UpdateAsync(id, request);
        if (notFound) return Results.NotFound();
        if (error is not null) return Results.BadRequest(error);
        return Results.Ok(response);
    }

    private static async Task<IResult> Delete(IG25EthnicityService service, int id)
    {
        var ok = await service.DeleteAsync(id);
        return ok ? Results.NoContent() : Results.NotFound();
    }
}
