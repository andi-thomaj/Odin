using System.Security.Claims;
using Odin.Api.Endpoints.PopulationManagement.Models;

namespace Odin.Api.Endpoints.PopulationManagement;

public static class PopulationEndpoints
{
    public static void MapPopulationEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/populations");

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

    private static async Task<IResult> GetAllAdmin(IPopulationService service)
    {
        var list = await service.GetAllAdminAsync();
        return Results.Ok(list);
    }

    private static async Task<IResult> GetByIdAdmin(IPopulationService service, int id)
    {
        var row = await service.GetByIdAdminAsync(id);
        return row is null ? Results.NotFound() : Results.Ok(row);
    }

    private static async Task<IResult> Create(HttpContext httpContext, IPopulationService service, CreatePopulationContract.Request request)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        var (response, error) = await service.CreateAsync(identityId, request);
        if (error is not null) return Results.BadRequest(error);
        return Results.Created($"/api/populations/admin/{response!.Id}", response);
    }

    private static async Task<IResult> Update(HttpContext httpContext, IPopulationService service, int id, UpdatePopulationContract.Request request)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        var (response, error, notFound) = await service.UpdateAsync(id, identityId, request);
        if (notFound) return Results.NotFound();
        if (error is not null) return Results.BadRequest(error);
        return Results.Ok(response);
    }

    private static async Task<IResult> Delete(IPopulationService service, int id)
    {
        var ok = await service.DeleteAsync(id);
        return ok ? Results.NoContent() : Results.NotFound();
    }

    private static string? ResolveIdentityId(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? httpContext.User.FindFirstValue("sub");
}
