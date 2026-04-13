using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Endpoints.G25SavedCoordinateManagement.Models;

namespace Odin.Api.Endpoints.G25SavedCoordinateManagement;

public static class G25SavedCoordinateEndpoints
{
    public static void MapG25SavedCoordinateEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/g25-saved-coordinates");

        endpoints.MapGet("/", GetAll)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated");

        endpoints.MapGet("/{id:int}", GetById)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated");

        endpoints.MapPost("/", Create)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("strict");

        endpoints.MapPut("/{id:int}", Update)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("strict");

        endpoints.MapDelete("/{id:int}", Delete)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("strict");
    }

    private static async Task<IResult> GetAll(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        IG25SavedCoordinateService service)
    {
        var userId = await ResolveUserId(httpContext, dbContext);
        if (userId is null) return Results.Unauthorized();

        var list = await service.GetAllForUserAsync(userId.Value);
        return Results.Ok(list);
    }

    private static async Task<IResult> GetById(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        IG25SavedCoordinateService service,
        int id)
    {
        var userId = await ResolveUserId(httpContext, dbContext);
        if (userId is null) return Results.Unauthorized();

        var row = await service.GetByIdForUserAsync(id, userId.Value);
        return row is null ? Results.NotFound() : Results.Ok(row);
    }

    private static async Task<IResult> Create(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        IG25SavedCoordinateService service,
        CreateG25SavedCoordinateContract.Request request)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        var userId = await ResolveUserId(httpContext, dbContext);
        if (userId is null) return Results.Unauthorized();

        var validation = Validate(request.Title, request.RawInput, request.AddMode, request.ViewId);
        if (validation is not null) return validation;

        var result = await service.CreateAsync(userId.Value, identityId, request);
        return Results.Created($"/api/g25-saved-coordinates/{result.Id}", result);
    }

    private static async Task<IResult> Update(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        IG25SavedCoordinateService service,
        int id,
        UpdateG25SavedCoordinateContract.Request request)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        var userId = await ResolveUserId(httpContext, dbContext);
        if (userId is null) return Results.Unauthorized();

        var validation = Validate(request.Title, request.RawInput, request.AddMode, request.ViewId);
        if (validation is not null) return validation;

        var result = await service.UpdateAsync(id, userId.Value, identityId, request);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> Delete(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        IG25SavedCoordinateService service,
        int id)
    {
        var userId = await ResolveUserId(httpContext, dbContext);
        if (userId is null) return Results.Unauthorized();

        var ok = await service.DeleteAsync(id, userId.Value);
        return ok ? Results.NoContent() : Results.NotFound();
    }

    private static IResult? Validate(string title, string rawInput, string addMode, string viewId)
    {
        if (string.IsNullOrWhiteSpace(title))
            return Results.BadRequest("Title is required.");
        if (string.IsNullOrWhiteSpace(rawInput))
            return Results.BadRequest("RawInput is required.");
        if (string.IsNullOrWhiteSpace(viewId))
            return Results.BadRequest("ViewId is required.");
        if (addMode is not ("aggregated" or "individual" or "as-one-group"))
            return Results.BadRequest("AddMode must be 'aggregated', 'individual', or 'as-one-group'.");
        return null;
    }

    private static string? ResolveIdentityId(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? httpContext.User.FindFirstValue("sub");

    private static async Task<int?> ResolveUserId(HttpContext httpContext, ApplicationDbContext dbContext)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (string.IsNullOrEmpty(identityId)) return null;

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.IdentityId == identityId);

        return user?.Id;
    }
}
