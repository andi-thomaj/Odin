using System.Security.Claims;
using Odin.Api.Endpoints.Admin.Models;
using Odin.Api.Endpoints.MergeManagement;

namespace Odin.Api.Endpoints.Admin;

/// <summary>
/// Admin endpoints for the one-click Panel Labels dev→prod promotion. <c>export</c> (run on dev)
/// returns a portable <see cref="PanelPromotionBundle"/> the Scientist/Admin downloads; <c>apply</c>
/// (run on prod) takes that uploaded bundle and previews it (<c>dryRun=true</c>, the default) or
/// applies it (<c>dryRun=false</c>). <b>AdminOnly</b> — applying rewrites the production panel's
/// labels (<c>.ind</c>) and sample→population links.
/// </summary>
public static class PanelPromotionEndpoints
{
    public static void MapPanelPromotionEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/admin/panel-promotion")
            .RequireAuthorization("AdminOnly");

        endpoints.MapGet("/export", Export)
            .Produces<PanelPromotionBundle>(StatusCodes.Status200OK);

        endpoints.MapPost("/apply", Apply)
            .Produces<PanelPromotionApplyResult>(StatusCodes.Status200OK);
    }

    /// <summary>Snapshot the current environment's panel labels + links into a downloadable bundle.</summary>
    private static async Task<IResult> Export(
        IPanelPromotionService promotion, string? panel, CancellationToken cancellationToken)
    {
        try
        {
            return Results.Ok(await promotion.ExportAsync(panel, cancellationToken));
        }
        catch (MergePipelineException ex)
        {
            // tools-api unreachable / .ind not provisioned — relay its status + detail.
            return Results.Problem(detail: ex.Detail, statusCode: (int)ex.StatusCode);
        }
    }

    /// <summary>
    /// Preview (default) or apply an uploaded bundle to the current environment. <c>dryRun=false</c>
    /// is required to actually write — omitting it previews, so an accidental call never mutates prod.
    /// </summary>
    private static async Task<IResult> Apply(
        IPanelPromotionService promotion, HttpContext httpContext,
        PanelPromotionBundle bundle, bool? dryRun, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub")
                         ?? "panel-promotion";
        try
        {
            var result = await promotion.ApplyAsync(bundle, identityId, dryRun ?? true, cancellationToken);
            return Results.Ok(result);
        }
        catch (MergePipelineException ex)
        {
            return Results.Problem(detail: ex.Detail, statusCode: (int)ex.StatusCode);
        }
    }
}
