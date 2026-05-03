using System.Security.Claims;

namespace Odin.Api.Endpoints.MediaManagement;

public static class MediaEndpoints
{
    public static void MapMediaEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/media");

        endpoints.MapGet("/audio/{musicTrackId:int}", DownloadAudio)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated");

        endpoints.MapPut("/audio/{musicTrackId:int}", UploadAudio)
            .DisableAntiforgery()
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("file-upload")
            .WithRequestTimeout(TimeSpan.FromMinutes(5));

        endpoints.MapPost("/audio/sync-from-disk", SyncMusicTrackAudioFromDisk)
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("strict")
            .WithRequestTimeout(TimeSpan.FromMinutes(10));

    }

    private static async Task<IResult> DownloadAudio(IMediaService service, int musicTrackId)
    {
        var result = await service.GetMusicTrackAudioAsync(musicTrackId);

        if (result is null)
            return Results.NotFound(new { Message = $"Audio file for music track {musicTrackId} not found." });

        var (data, contentType, fileName) = result.Value;
        return Results.File(data, contentType, fileName,
            lastModified: null,
            entityTag: null,
            enableRangeProcessing: true);
    }

    private static async Task<IResult> UploadAudio(IMediaService service, HttpContext httpContext, int musicTrackId, IFormFile file)
    {
        var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub")
                         ?? string.Empty;

        var (success, error) = await service.UploadMusicTrackAudioAsync(musicTrackId, file, identityId);

        return success
            ? Results.NoContent()
            : Results.BadRequest(new { Message = error });
    }

    private static async Task<IResult> SyncMusicTrackAudioFromDisk(IMediaService service, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub")
                         ?? string.Empty;

        var (updated, missingOnDisk, failed, unmatched, firstError) =
            await service.SyncMusicTrackAudioFromDiskAsync(identityId, cancellationToken);

        return Results.Ok(new { Updated = updated, MissingOnDisk = missingOnDisk, Failed = failed, Unmatched = unmatched, FirstError = firstError });
    }

}
