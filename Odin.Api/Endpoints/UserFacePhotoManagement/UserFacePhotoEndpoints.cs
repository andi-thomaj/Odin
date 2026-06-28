using Odin.Api.Authentication;
using Odin.Api.Endpoints.UserFacePhotoManagement.Models;

namespace Odin.Api.Endpoints.UserFacePhotoManagement;

/// <summary>
/// Per-user (identity-scoped) face-photo set endpoints (<c>/v1/api/users/face-photos</c>). The iOS app captures a
/// guided multi-angle selfie set via ARKit and uploads it here for future AI-image generation. Bytes live in R2
/// (private); a photo's pixels are served ONLY via the authenticated <c>/{id}/download</c> route — never a public URL,
/// because face data is biometric. Upload is <b>replace-set</b> (idempotent re-upload).
/// </summary>
public static class UserFacePhotoEndpoints
{
    public static void MapUserFacePhotoEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/users/face-photos").RequireAuthorization("EmailVerified");

        endpoints.MapPost("/", Upload)
            .DisableAntiforgery()
            .RequireRateLimiting("file-upload")
            .Produces<FacePhotoSetContract.Response>(StatusCodes.Status201Created)
            .WithRequestTimeout(TimeSpan.FromMinutes(5));

        endpoints.MapGet("/", GetSet)
            .RequireRateLimiting("authenticated")
            .Produces<FacePhotoSetContract.Response>(StatusCodes.Status200OK);

        endpoints.MapGet("/{id:int}/download", Download)
            .RequireRateLimiting("authenticated");

        endpoints.MapDelete("/{id:int}", DeleteOne)
            .RequireRateLimiting("strict")
            .Produces(StatusCodes.Status204NoContent);

        endpoints.MapDelete("/", ClearAll)
            .RequireRateLimiting("strict")
            .Produces(StatusCodes.Status204NoContent);
    }

    // Multipart batch is read from the form directly (a `List<IFormFile>` doesn't model-bind cleanly in minimal APIs):
    // files come under the "Photos" field, with a "CaptureSessionId" GUID for idempotency.
    private static async Task<IResult> Upload(IUserFacePhotoService service, HttpContext httpContext, CancellationToken cancellationToken)
    {
        if (!httpContext.Request.HasFormContentType)
            return Results.BadRequest(new { Message = "Expected a multipart/form-data upload." });

        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        var form = await httpContext.Request.ReadFormAsync(cancellationToken);

        var files = form.Files.Where(f => f.Name == "Photos").ToList();
        if (files.Count == 0)
            files = form.Files.ToList(); // tolerate clients that don't name the field "Photos"

        var sessionId = Guid.TryParse(form["CaptureSessionId"].ToString(), out var parsed) ? parsed : Guid.NewGuid();

        var (response, error) = await service.ReplaceSetAsync(files, sessionId, identityId, cancellationToken);
        return error is not null
            ? Results.BadRequest(new { Message = error })
            : Results.Created("/v1/api/users/face-photos", response);
    }

    private static async Task<IResult> GetSet(IUserFacePhotoService service, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        return Results.Ok(await service.GetSetAsync(identityId, cancellationToken));
    }

    private static async Task<IResult> Download(IUserFacePhotoService service, HttpContext httpContext, int id, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        var (bytes, contentType, statusCode) = await service.GetPhotoBytesAsync(id, identityId, cancellationToken);
        return statusCode switch
        {
            200 => Results.File(bytes!, contentType ?? "image/jpeg"),
            403 => Results.Forbid(),
            _ => Results.NotFound(),
        };
    }

    private static async Task<IResult> DeleteOne(IUserFacePhotoService service, HttpContext httpContext, int id, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        var statusCode = await service.DeletePhotoAsync(id, identityId, cancellationToken);
        return statusCode switch
        {
            204 => Results.NoContent(),
            403 => Results.Forbid(),
            _ => Results.NotFound(),
        };
    }

    private static async Task<IResult> ClearAll(IUserFacePhotoService service, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.GetIdentityId() ?? string.Empty;
        await service.ClearSetAsync(identityId, cancellationToken);
        return Results.NoContent();
    }
}
