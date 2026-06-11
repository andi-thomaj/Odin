using System.Security.Claims;
using Odin.Api.Endpoints.Admin.Models;

namespace Odin.Api.Endpoints.Admin;

/// <summary>
/// Admin-only promotion of Panel Labels edits (sample→population links + population labels) between
/// environments. The dev snapshot is produced + committed by the <c>PanelPromotionSnapshotExportTests</c>
/// seed-export utility; on the target, <c>POST import</c> mirrors links + applies label diffs (the
/// startup seeder also applies links on deploy).
/// </summary>
public static class PanelPromotionEndpoints
{
    public static void MapPanelPromotionEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/admin/panel-promotion")
            .RequireAuthorization("AdminOnly");

        endpoints.MapPost("/import", Import)
            .RequireRateLimiting("strict")
            .Produces<PanelPromotionImportContract.Response>(StatusCodes.Status200OK)
            .WithRequestTimeout(TimeSpan.FromMinutes(5));
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
