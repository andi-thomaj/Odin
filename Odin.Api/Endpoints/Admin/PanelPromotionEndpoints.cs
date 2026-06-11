using System.Security.Claims;
using Odin.Api.Endpoints.Admin.Models;

namespace Odin.Api.Endpoints.Admin;

/// <summary>
/// Admin-only promotion of Panel Labels edits (sample→population links + population labels) between
/// environments via committed snapshot files. On dev: <c>GET export</c> downloads the artifacts to
/// commit. On the target: <c>POST import</c> mirrors links + applies label diffs (also run by the
/// startup seeder for links only).
/// </summary>
public static class PanelPromotionEndpoints
{
    public static void MapPanelPromotionEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/admin/panel-promotion")
            .RequireAuthorization("AdminOnly");

        endpoints.MapGet("/export", Export)
            .RequireRateLimiting("authenticated")
            .Produces<PanelPromotionExportContract.Response>(StatusCodes.Status200OK);

        endpoints.MapPost("/import", Import)
            .RequireRateLimiting("strict")
            .Produces<PanelPromotionImportContract.Response>(StatusCodes.Status200OK)
            .WithRequestTimeout(TimeSpan.FromMinutes(5));
    }

    private static async Task<IResult> Export(
        IPanelPromotionService service, string panel = "HO", CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(panel)) return Results.BadRequest("Panel is required.");
        var result = await service.ExportAsync(panel, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task<IResult> Import(
        IPanelPromotionService service, HttpContext httpContext, CancellationToken cancellationToken)
    {
        var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? httpContext.User.FindFirstValue("sub")
                         ?? string.Empty;

        var result = await service.ImportAsync(identityId, cancellationToken);
        return Results.Ok(result);
    }
}
