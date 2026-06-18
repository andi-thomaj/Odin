using System.Net;
using Microsoft.AspNetCore.Mvc;
using Odin.Api.Endpoints.CladeFinderManagement.Models;
using Odin.Api.Endpoints.HaplogroupHeatmap;
using Odin.Api.Endpoints.HaplogroupHeatmap.Models;
using Odin.Api.Extensions;

namespace Odin.Api.Endpoints.CladeFinderManagement
{
    public static class CladeFinderEndpoints
    {
        public static void MapCladeFinderEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/clade-finder");

            endpoints.MapPost("/analyze", Analyze)
                .DisableAntiforgery()
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("file-upload")
                .Produces<AnalyzeCladeContract.Response>(StatusCodes.Status200OK)
                .WithRequestTimeout(TimeSpan.FromMinutes(5));

            // Geographic distribution (ancient + modern) + migration path for a clade — drives the
            // result-page heatmap. Read-only, cached per clade; served from the imported reference tables.
            endpoints.MapGet("/distribution", GetDistribution)
                .RequireAuthorization("EmailVerified")
                .Produces<HaplogroupDistributionContract.Response>(StatusCodes.Status200OK);

            // Smooth kernel-interpolated relative-frequency surface (the heatmap's 3rd mode). Computed live
            // by odin-tools-api; anchored + cached here per (clade, layer, radius).
            endpoints.MapGet("/relative-frequency", GetRelativeFrequency)
                .RequireAuthorization("EmailVerified")
                .Produces<RelativeFrequencyContract.Response>(StatusCodes.Status200OK);
        }

        private static async Task<IResult> GetRelativeFrequency(
            IHaplogroupRelativeFrequencyService service,
            [FromQuery] string clade,
            [FromQuery] string? layer,
            [FromQuery] double? radiusKm,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(clade))
            {
                return Results.BadRequest(new { Message = "A clade is required." });
            }

            try
            {
                var response = await service.GetAsync(
                    clade, layer ?? "ancient", radiusKm ?? 300.0, cancellationToken);
                return Results.Ok(response);
            }
            catch (HttpRequestException)
            {
                // The grid is computed by odin-tools-api; if it's unreachable, fail soft so the other
                // heatmap modes keep working and the FE can show a "surface unavailable" state.
                return Results.Problem(
                    detail: "The relative-frequency service is unavailable.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        }

        private static async Task<IResult> GetDistribution(
            IHaplogroupDistributionService service,
            [FromQuery] string clade,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(clade))
            {
                return Results.BadRequest(new { Message = "A clade is required." });
            }

            var response = await service.GetAsync(clade, cancellationToken);
            return Results.Ok(response);
        }

        private static async Task<IResult> Analyze(
            ICladeFinderService service,
            IFormFile file,
            [FromForm] string? build,
            CancellationToken cancellationToken)
        {
            var request = new AnalyzeCladeContract.Request { File = file, Build = build };

            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            try
            {
                var response = await service.AnalyzeAsync(file, build, cancellationToken);
                return Results.Ok(response);
            }
            catch (CladeFinderException ex)
            {
                return ex.StatusCode switch
                {
                    HttpStatusCode.BadRequest => Results.BadRequest(new { Message = ex.Detail }),
                    HttpStatusCode.ServiceUnavailable => Results.Problem(
                        detail: ex.Detail, statusCode: StatusCodes.Status503ServiceUnavailable),
                    _ => Results.Problem(
                        detail: "The clade finder service returned an error.",
                        statusCode: StatusCodes.Status502BadGateway),
                };
            }
        }
    }
}
