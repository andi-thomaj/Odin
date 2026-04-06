using System.Security.Claims;
using Odin.Api.Endpoints.ChangelogManagement.Models;

namespace Odin.Api.Endpoints.ChangelogManagement;

public static class ChangelogEndpoints
{
    public static void MapChangelogEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/changelog");

        endpoints.MapGet("/", GetPublished)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated");

        endpoints.MapGet("/all", GetAllForAdmin)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict");

        endpoints.MapPost("/versions", CreateVersion)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict");

        endpoints.MapPut("/versions/{id:int}", UpdateVersion)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict");

        endpoints.MapDelete("/versions/{id:int}", DeleteVersion)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict");

        endpoints.MapPost("/versions/{versionId:int}/entries", CreateEntry)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict");

        endpoints.MapPut("/entries/{id:int}", UpdateEntry)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict");

        endpoints.MapDelete("/entries/{id:int}", DeleteEntry)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict");
    }

    private static async Task<IResult> GetPublished(IChangelogService service)
    {
        var list = await service.GetPublishedAsync();
        return Results.Ok(list);
    }

    private static async Task<IResult> GetAllForAdmin(IChangelogService service)
    {
        var list = await service.GetAllForAdminAsync();
        return Results.Ok(list);
    }

    private static async Task<IResult> CreateVersion(
        HttpContext httpContext,
        IChangelogService service,
        CreateVersionContract.Request request)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Version) || string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest("Version and Title are required.");

        var result = await service.CreateVersionAsync(identityId, request);
        return Results.Created($"/api/changelog/versions/{result.Id}", result);
    }

    private static async Task<IResult> UpdateVersion(
        HttpContext httpContext,
        IChangelogService service,
        int id,
        UpdateVersionContract.Request request)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Version) || string.IsNullOrWhiteSpace(request.Title))
            return Results.BadRequest("Version and Title are required.");

        var result = await service.UpdateVersionAsync(id, identityId, request);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> DeleteVersion(IChangelogService service, int id)
    {
        var ok = await service.DeleteVersionAsync(id);
        return ok ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> CreateEntry(
        HttpContext httpContext,
        IChangelogService service,
        int versionId,
        CreateEntryContract.Request request)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Description))
            return Results.BadRequest("Description is required.");

        if (!ChangelogService.IsValidEntryType(request.Type))
            return Results.BadRequest("Type must be Feature, BugFix, or Improvement.");

        var result = await service.CreateEntryAsync(versionId, identityId, request);
        if (result is null) return Results.NotFound();
        return Results.Created($"/api/changelog/entries/{result.Id}", result);
    }

    private static async Task<IResult> UpdateEntry(
        HttpContext httpContext,
        IChangelogService service,
        int id,
        UpdateEntryContract.Request request)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        if (string.IsNullOrWhiteSpace(request.Description))
            return Results.BadRequest("Description is required.");

        if (!ChangelogService.IsValidEntryType(request.Type))
            return Results.BadRequest("Type must be Feature, BugFix, or Improvement.");

        var result = await service.UpdateEntryAsync(id, identityId, request);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }

    private static async Task<IResult> DeleteEntry(IChangelogService service, int id)
    {
        var ok = await service.DeleteEntryAsync(id);
        return ok ? Results.NoContent() : Results.NotFound();
    }

    private static string? ResolveIdentityId(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? httpContext.User.FindFirstValue("sub");
}
