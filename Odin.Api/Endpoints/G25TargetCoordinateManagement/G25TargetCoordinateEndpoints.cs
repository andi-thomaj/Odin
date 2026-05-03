using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Endpoints.G25TargetCoordinateManagement.Models;

namespace Odin.Api.Endpoints.G25TargetCoordinateManagement;

public static class G25TargetCoordinateEndpoints
{
    private const int MaxCoordinatesBytes = 5 * 1024 * 1024;

    public static void MapG25TargetCoordinateEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/g25-target-coordinates");

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
        IG25TargetCoordinateService service)
    {
        var userId = await ResolveUserId(httpContext, dbContext);
        if (userId is null) return Results.Unauthorized();

        var list = await service.GetAllForUserAsync(userId.Value);
        return Results.Ok(list);
    }

    private static async Task<IResult> GetById(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        IG25TargetCoordinateService service,
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
        IG25TargetCoordinateService service,
        CreateG25TargetCoordinateContract.Request request)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        var userId = await ResolveUserId(httpContext, dbContext);
        if (userId is null) return Results.Unauthorized();

        var validation = ValidateCreate(request);
        if (validation is not null) return validation;

        var result = await service.CreateAsync(userId.Value, identityId, request);
        return Results.Created($"/api/g25-target-coordinates/{result.Id}", result);
    }

    private static async Task<IResult> Update(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        IG25TargetCoordinateService service,
        int id,
        UpdateG25TargetCoordinateContract.Request request)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        var userId = await ResolveUserId(httpContext, dbContext);
        if (userId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Label))
            return Results.BadRequest("Label is required.");
        if (string.IsNullOrEmpty(request.Coordinates))
            return Results.BadRequest("Coordinates are required.");
        if (System.Text.Encoding.UTF8.GetByteCount(request.Coordinates) > MaxCoordinatesBytes)
            return Results.BadRequest("Coordinates exceed maximum allowed size.");

        var result = await service.UpdateAsync(id, userId.Value, identityId, request);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> Delete(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        IG25TargetCoordinateService service,
        int id)
    {
        var userId = await ResolveUserId(httpContext, dbContext);
        if (userId is null) return Results.Unauthorized();

        var ok = await service.DeleteAsync(id, userId.Value);
        return ok ? Results.NoContent() : Results.NotFound();
    }

    private static IResult? ValidateCreate(CreateG25TargetCoordinateContract.Request request)
    {
        if (string.IsNullOrWhiteSpace(request.Label))
            return Results.BadRequest("Label is required.");
        if (string.IsNullOrEmpty(request.Coordinates))
            return Results.BadRequest("Coordinates are required.");
        if (System.Text.Encoding.UTF8.GetByteCount(request.Coordinates) > MaxCoordinatesBytes)
            return Results.BadRequest("Coordinates exceed maximum allowed size.");
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
