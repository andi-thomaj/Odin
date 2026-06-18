using Odin.Api.Endpoints.MergeManagement;

namespace Odin.Api.Endpoints.Admin;

/// <summary>
/// Admin-only operations on the AADR merge queue. Merges run strictly one at a time with <b>no
/// automatic retries</b> (the 2M-panel merge is memory-heavy), so a failed merge stays <c>Failed</c>
/// until an admin re-runs it here. Surfaced as a "Retry" action on the Ancient Origins results table.
/// </summary>
public static class MergeAdminEndpoints
{
    public static void MapMergeAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/admin/merge")
            .RequireAuthorization("AdminOnly");

        endpoints.MapPost("/{rawGeneticFileId:int}/retry", RetryMerge)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        endpoints.MapPost("/{rawGeneticFileId:int}/stop", StopMerge)
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);
    }

    private static async Task<IResult> RetryMerge(
        int rawGeneticFileId, IMergeJob mergeJob, CancellationToken cancellationToken)
    {
        try
        {
            await mergeJob.RequeueAsync(rawGeneticFileId, cancellationToken);
            return Results.Ok(new { rawGeneticFileId, requeued = true });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(ex.Message);
        }
    }

    private static async Task<IResult> StopMerge(
        int rawGeneticFileId, IMergeJob mergeJob, CancellationToken cancellationToken)
    {
        try
        {
            await mergeJob.StopAsync(rawGeneticFileId, cancellationToken);
            return Results.Ok(new { rawGeneticFileId, stopped = true });
        }
        catch (KeyNotFoundException ex)
        {
            return Results.NotFound(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Results.Conflict(ex.Message);
        }
    }
}
