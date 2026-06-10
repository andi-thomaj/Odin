using Microsoft.AspNetCore.Http.Features;
using Odin.Api.Endpoints.MergeManagement;

namespace Odin.Api.Endpoints.Admin;

/// <summary>
/// Admin-only proxy for restoring the pre-built AADR merge panel (<c>v66_2M_aadr_PUB</c>) onto the
/// tools-api volume after a crash/redeploy. Streams each packed file (geno/snp/ind) through to the
/// tools-api staging area, then activates the triplet atomically. Surfaced as the "Restore merge
/// panel" admin page in the frontend.
///
/// The upload route lifts the global 50&#160;MB request-body cap and runs under a long
/// <c>PanelRestore</c> request-timeout policy, because the files are multi-GB. The body is streamed
/// (never buffered/spooled) on the .NET side; see <see cref="MergePipelineService"/>.
/// </summary>
public static class MergePanelAdminEndpoints
{
    public static void MapMergePanelAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/admin/merge-panel")
            .RequireAuthorization("AdminOnly");

        endpoints.MapGet("/status", GetStatus)
            .Produces<PanelStatusResult>(StatusCodes.Status200OK);

        endpoints.MapPost("/upload", UploadFile)
            .Produces<PanelUploadResult>(StatusCodes.Status200OK)
            .WithRequestTimeout("PanelRestore");

        endpoints.MapPost("/activate", Activate)
            .Produces<PanelActivateResult>(StatusCodes.Status200OK)
            .WithRequestTimeout("PanelRestore");
    }

    private static Task<IResult> GetStatus(
        IMergePipelineService merge, string? panel, CancellationToken cancellationToken)
        => Proxy(async () => Results.Ok(await merge.GetPanelStatusAsync(panel, cancellationToken)));

    private static Task<IResult> UploadFile(
        HttpContext context, IMergePipelineService merge,
        string ext, string? panel, string? sha256, CancellationToken cancellationToken)
    {
        // Lift Kestrel's global 50 MB body cap for this route only (panels are multi-GB). Must run
        // before the body is read; this handler is reached before context.Request.Body is consumed.
        var sizeFeature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (sizeFeature is { IsReadOnly: false })
            sizeFeature.MaxRequestBodySize = null;

        return Proxy(async () => Results.Ok(
            await merge.UploadPanelFileAsync(ext, panel, sha256, context.Request.Body, cancellationToken)));
    }

    private static Task<IResult> Activate(
        IMergePipelineService merge, string? panel, bool? force, CancellationToken cancellationToken)
        => Proxy(async () => Results.Ok(await merge.ActivatePanelAsync(panel, force ?? false, cancellationToken)));

    /// <summary>
    /// Run a tools-api proxy call, mapping failures to a clean response: the upstream status + detail
    /// for a <see cref="MergePipelineException"/> (e.g. the 422 validation message), or a 502 when the
    /// tools API is unreachable or drops the connection mid-stream (common when the upstream rejects a
    /// large upload early — surfaces as a "broken pipe" while .NET is still sending the body).
    /// </summary>
    private static async Task<IResult> Proxy(Func<Task<IResult>> action)
    {
        try
        {
            return await action();
        }
        catch (MergePipelineException ex)
        {
            return Results.Problem(detail: ex.Detail, statusCode: (int)ex.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            return Results.Problem(
                detail: "Could not reach the tools API, or it rejected the request mid-upload "
                    + $"(check the tools-api logs — e.g. AADR_DIR not writable): {ex.Message}",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
