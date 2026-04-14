using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.AdmixtureSavedFileManagement.Models;

namespace Odin.Api.Endpoints.AdmixtureSavedFileManagement;

public static class AdmixtureSavedFileEndpoints
{
    private const int MaxContentBytes = 5 * 1024 * 1024;

    public static void MapAdmixtureSavedFileEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/admixture-saved-files");

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
        IAdmixtureSavedFileService service,
        string? kind)
    {
        var userId = await ResolveUserId(httpContext, dbContext);
        if (userId is null) return Results.Unauthorized();

        var resolvedKind = NormalizeKind(kind);
        if (resolvedKind is null) return Results.BadRequest("Invalid kind.");

        var list = await service.GetAllForUserAsync(userId.Value, resolvedKind);
        return Results.Ok(list);
    }

    private static async Task<IResult> GetById(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        IAdmixtureSavedFileService service,
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
        IAdmixtureSavedFileService service,
        CreateAdmixtureSavedFileContract.Request request)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        var userId = await ResolveUserId(httpContext, dbContext);
        if (userId is null) return Results.Unauthorized();

        var validation = ValidateCreate(request);
        if (validation is not null) return validation;

        var resolvedKind = NormalizeKind(request.Kind);
        if (resolvedKind is null) return Results.BadRequest("Invalid kind.");

        var result = await service.CreateAsync(userId.Value, identityId, request, resolvedKind);
        return Results.Created($"/api/admixture-saved-files/{result.Id}", result);
    }

    private static async Task<IResult> Update(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        IAdmixtureSavedFileService service,
        int id,
        UpdateAdmixtureSavedFileContract.Request request)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        var userId = await ResolveUserId(httpContext, dbContext);
        if (userId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest("Title is required.");
        if (string.IsNullOrEmpty(request.Content))
            return Results.BadRequest("Content is required.");
        if (System.Text.Encoding.UTF8.GetByteCount(request.Content) > MaxContentBytes)
            return Results.BadRequest("Content exceeds maximum allowed size.");

        var result = await service.UpdateAsync(id, userId.Value, identityId, request);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> Delete(
        HttpContext httpContext,
        ApplicationDbContext dbContext,
        IAdmixtureSavedFileService service,
        int id)
    {
        var userId = await ResolveUserId(httpContext, dbContext);
        if (userId is null) return Results.Unauthorized();

        var ok = await service.DeleteAsync(id, userId.Value);
        return ok ? Results.NoContent() : Results.NotFound();
    }

    private static IResult? ValidateCreate(CreateAdmixtureSavedFileContract.Request request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest("Title is required.");
        if (string.IsNullOrEmpty(request.Content))
            return Results.BadRequest("Content is required.");
        if (System.Text.Encoding.UTF8.GetByteCount(request.Content) > MaxContentBytes)
            return Results.BadRequest("Content exceeds maximum allowed size.");
        return null;
    }

    private static string? NormalizeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind)) return AdmixtureSavedFileKind.Source;
        var lower = kind.Trim().ToLowerInvariant();
        return AdmixtureSavedFileKind.IsValid(lower) ? lower : null;
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
