using System.Net;
using Microsoft.AspNetCore.Mvc;
using Odin.Api.Endpoints.CladeFinderManagement.Models;
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
