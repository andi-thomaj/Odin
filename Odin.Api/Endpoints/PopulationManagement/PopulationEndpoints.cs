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

        endpoints.MapGet("/video-avatars", GetVideoAvatarsList)
            .AllowAnonymous()
            .RequireRateLimiting("authenticated");

        // GET /{id:int}/video-avatar removed: avatars are now served by Cloudflare R2 directly
        // at avatars.ancestrify.io/populations/{id}.mp4. Frontend uses the URL returned by the
        // /video-avatars list endpoint.

        endpoints.MapPut("/{id:int}/video-avatar", UploadVideoAvatar)
            .DisableAntiforgery()
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("file-upload")
            .WithRequestTimeout(TimeSpan.FromMinutes(5));

        endpoints.MapDelete("/{id:int}/video-avatar", DeleteVideoAvatar)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict");

        endpoints.MapPost("/video-avatars/sync-from-disk", SyncVideoAvatarsFromDisk)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict")
            .WithRequestTimeout(TimeSpan.FromMinutes(10));
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

    private static async Task<IResult> GetVideoAvatarsList(IPopulationService service, CancellationToken cancellationToken)
    {
        var list = await service.GetVideoAvatarsListAsync(cancellationToken);
        return Results.Ok(list);
    }

    private static async Task<IResult> UploadVideoAvatar(HttpContext httpContext, IPopulationService service, int id, IFormFile file, CancellationToken cancellationToken)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        var (success, error, notFound) = await service.UploadVideoAvatarAsync(id, file, identityId, cancellationToken);
        if (notFound) return Results.NotFound();
        return success
            ? Results.NoContent()
            : Results.BadRequest(new { Message = error });
    }

    private static async Task<IResult> DeleteVideoAvatar(HttpContext httpContext, IPopulationService service, int id, CancellationToken cancellationToken)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        var ok = await service.DeleteVideoAvatarAsync(id, identityId, cancellationToken);
        return ok ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> SyncVideoAvatarsFromDisk(HttpContext httpContext, IPopulationService service, CancellationToken cancellationToken)
    {
        var identityId = ResolveIdentityId(httpContext);
        if (identityId is null) return Results.Unauthorized();

        var (updated, unmatched, missingOnDisk, failed) = await service.SyncVideoAvatarsFromDiskAsync(identityId, cancellationToken);
        return Results.Ok(new { Updated = updated, Unmatched = unmatched, MissingOnDisk = missingOnDisk, Failed = failed });
    }

    private static string? ResolveIdentityId(HttpContext httpContext) =>
        httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? httpContext.User.FindFirstValue("sub");
}
