using Odin.Api.Endpoints.G25Calculations.Models;

namespace Odin.Api.Endpoints.G25Calculations;

public static class G25CalculationEndpoints
{
    public static void MapG25CalculationEndpoints(this IEndpointRouteBuilder app)
    {
        var endpoints = app.MapGroup("api/g25-calculations");

        endpoints.MapPost("/distances", ComputeDistances)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("strict");

        endpoints.MapPost("/admixture/single", ComputeAdmixtureSingle)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("strict");

        endpoints.MapPost("/admixture/multi", ComputeAdmixtureMulti)
            .RequireAuthorization("EmailVerified")
            .RequireRateLimiting("strict");
    }

    private static async Task<IResult> ComputeDistances(
        IG25CalculationService service,
        ComputeDistancesContract.Request request,
        CancellationToken ct)
    {
        var (response, error, notFound) = await service.ComputeDistancesAsync(request, ct);
        if (notFound) return Results.NotFound();
        if (error is not null) return Results.BadRequest(error);
        return Results.Ok(response);
    }

    private static async Task<IResult> ComputeAdmixtureSingle(
        IG25CalculationService service,
        ComputeAdmixtureSingleContract.Request request,
        CancellationToken ct)
    {
        var (response, error, notFound) = await service.ComputeAdmixtureSingleAsync(request, ct);
        if (notFound) return Results.NotFound();
        if (error is not null) return Results.BadRequest(error);
        return Results.Ok(response);
    }

    private static async Task<IResult> ComputeAdmixtureMulti(
        IG25CalculationService service,
        ComputeAdmixtureMultiContract.Request request,
        CancellationToken ct)
    {
        var (response, error) = await service.ComputeAdmixtureMultiAsync(request, ct);
        if (error is not null) return Results.BadRequest(error);
        return Results.Ok(response);
    }
}
