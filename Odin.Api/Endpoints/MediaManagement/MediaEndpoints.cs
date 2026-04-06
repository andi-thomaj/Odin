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

        endpoints.MapGet("/video/{populationId:int}", DownloadVideo)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated");

        endpoints.MapPut("/audio/{musicTrackId:int}", UploadAudio)
            .DisableAntiforgery()
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("file-upload")
            .WithRequestTimeout(TimeSpan.FromMinutes(5));

        endpoints.MapPut("/video/{populationId:int}", UploadVideo)
            .DisableAntiforgery()
            .RequireAuthorization("AdminOnly")
            .RequireRateLimiting("file-upload")
            .WithRequestTimeout(TimeSpan.FromMinutes(5));
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

    private static async Task<IResult> DownloadVideo(IMediaService service, int populationId)
    {
        var result = await service.GetPopulationVideoAsync(populationId);

        if (result is null)
            return Results.NotFound(new { Message = $"Video file for population {populationId} not found." });

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

    private static async Task<IResult> UploadVideo(IMediaService service, HttpContext httpContext, int populationId, IFormFile file)
    {
        var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub")
                         ?? string.Empty;

        var (success, error) = await service.UploadPopulationVideoAsync(populationId, file, identityId);

        return success
            ? Results.NoContent()
            : Results.BadRequest(new { Message = error });
    }
}
