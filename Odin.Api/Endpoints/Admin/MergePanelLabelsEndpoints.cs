using Odin.Api.Endpoints.MergeManagement;

namespace Odin.Api.Endpoints.Admin;

/// <summary>
/// Scientist/Admin endpoints to view and edit the AADR merge panel's sample population labels
/// (column 3 of the panel's <c>.ind</c>) — correcting a mislabeled sample without re-uploading the
/// multi-GB panel. Proxies the tools-api <c>/v1/merge/panel/ind*</c> routes. Surfaced as the
/// "Panel labels" page in the frontend.
///
/// Editing is text-only on the population label and order-preserving on the tools-api side (the
/// <c>.geno</c> matrix is keyed by row position); these endpoints just relay.
/// </summary>
public static class MergePanelLabelsEndpoints
{
    public static void MapMergePanelLabelsEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/merge-panel/labels")
            .RequireAuthorization("ScientistOrAdmin");

        endpoints.MapGet("/", GetRows)
            .Produces<PanelIndRowsResult>(StatusCodes.Status200OK);

        endpoints.MapPut("/row", SetRowLabel)
            .Produces<PanelIndRowResult>(StatusCodes.Status200OK);

        endpoints.MapPost("/rename", RenameLabel)
            .Produces<PanelRenameLabelResult>(StatusCodes.Status200OK);
    }

    private static Task<IResult> GetRows(
        IMergePipelineService merge, string? panel, CancellationToken cancellationToken)
        => Proxy(async () => Results.Ok(await merge.GetPanelIndRowsAsync(panel, cancellationToken)));

    private static Task<IResult> SetRowLabel(
        IMergePipelineService merge, int index, string label, string? panel, CancellationToken cancellationToken)
        => Proxy(async () => Results.Ok(
            await merge.SetPanelIndRowLabelAsync(panel, index, label, cancellationToken)));

    private static Task<IResult> RenameLabel(
        IMergePipelineService merge, string fromLabel, string toLabel, string? panel, CancellationToken cancellationToken)
        => Proxy(async () => Results.Ok(
            await merge.RenamePanelLabelAsync(panel, fromLabel, toLabel, cancellationToken)));

    /// <summary>Relay the tools-api status + detail (e.g. the 422 validation message) to the caller,
    /// or a 502 when the tools API is unreachable.</summary>
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
                detail: $"Could not reach the tools API: {ex.Message}",
                statusCode: StatusCodes.Status502BadGateway);
        }
    }
}
