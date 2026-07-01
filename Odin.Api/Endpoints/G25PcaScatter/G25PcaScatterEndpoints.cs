using Odin.Api.Endpoints.G25PcaScatter.Models;

namespace Odin.Api.Endpoints.G25PcaScatter;

public static class G25PcaScatterEndpoints
{
    public static void MapG25PcaScatterEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/g25-pca");

        // Owner-facing per-era PCA scatter for the Ancient Origins PCA tab. The reference cloud is
        // generic public reference data (no per-customer content), so EmailVerified matches the sibling
        // g25-distance-eras endpoint; the user's own coordinate is projected client-side via the basis.
        endpoints.MapGet("/eras/{eraId:int}", GetEraScatter)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("authenticated")
            .Produces<PcaEraScatterContract.Response>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }

    private static async Task<IResult> GetEraScatter(
        IG25PcaScatterService service, int eraId, CancellationToken cancellationToken)
    {
        var result = await service.GetEraScatterAsync(eraId, cancellationToken);
        return result is null ? Results.NotFound() : Results.Ok(result);
    }
}
