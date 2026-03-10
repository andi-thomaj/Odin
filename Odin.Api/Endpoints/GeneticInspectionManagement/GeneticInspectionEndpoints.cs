using Microsoft.AspNetCore.Mvc;
using Odin.Api.Endpoints.GeneticInspectionManagement.Models;
using Odin.Api.Endpoints.RawGeneticFileManagement.Models;
using Odin.Api.Extensions;

namespace Odin.Api.Endpoints.GeneticInspectionManagement
{
    public static class GeneticInspectionEndpoints
    {
        public static void MapGeneticInspectionEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/genetic-inspections");

            endpoints.MapGet("/", GetAll).RequireAuthorization("Authenticated");
            endpoints.MapGet("/{id:int}", GetById).RequireAuthorization("Authenticated");
            endpoints.MapPost("/", Create).RequireAuthorization("ScientistOrAdmin");
            endpoints.MapDelete("/{id:int}", Delete).RequireAuthorization("AdminOnly");

            endpoints.MapPost("/{id:int}/genetic-file", UploadGeneticFile).DisableAntiforgery()
                .RequireAuthorization("ScientistOrAdmin");
            endpoints.MapGet("/{id:int}/genetic-file/download", DownloadGeneticFile)
                .RequireAuthorization("Authenticated");
            endpoints.MapDelete("/{id:int}/genetic-file", DeleteGeneticFile).RequireAuthorization("Authenticated");

            endpoints.MapGet("/{id:int}/qpadm-result", GetQpadmResult).RequireAuthorization("ScientistOrAdmin");
            endpoints.MapPost("/{id:int}/qpadm-result", SubmitQpadmResult).RequireAuthorization("ScientistOrAdmin");
            endpoints.MapPost("/{id:int}/vahaduo-result", SubmitVahaduoResult).RequireAuthorization("ScientistOrAdmin");
        }

        private static async Task<IResult> GetAll(IGeneticInspectionService service)
        {
            var inspections = await service.GetAllAsync();
            return Results.Ok(inspections);
        }

        private static async Task<IResult> GetById(IGeneticInspectionService service, int id)
        {
            var inspection = await service.GetByIdAsync(id);

            return inspection is null
                ? Results.NotFound(new { Message = $"Genetic inspection with ID {id} not found." })
                : Results.Ok(inspection);
        }

        private static async Task<IResult> Create(
            IGeneticInspectionService service,
            [FromBody] CreateGeneticInspectionContract.Request request)
        {
            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            var response = await service.CreateAsync(request);
            return Results.Created($"/api/genetic-inspections/{response.Id}", response);
        }

        private static async Task<IResult> Delete(IGeneticInspectionService service, int id)
        {
            var deleted = await service.DeleteAsync(id);

            return deleted
                ? Results.NoContent()
                : Results.NotFound(new { Message = $"Genetic inspection with ID {id} not found." });
        }

        private static async Task<IResult> UploadGeneticFile(IGeneticInspectionService service, int id, IFormFile file)
        {
            var request = new UploadGeneticFileContract.Request { File = file };

            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            var response = await service.UploadGeneticFileAsync(id, request);

            return response is null
                ? Results.NotFound(new { Message = $"Genetic inspection with ID {id} not found." })
                : Results.Created($"/api/genetic-inspections/{id}/genetic-file", response);
        }

        private static async Task<IResult> DownloadGeneticFile(IGeneticInspectionService service, int id)
        {
            var result = await service.DownloadGeneticFileAsync(id);

            if (result is null)
            {
                return Results.NotFound(new { Message = $"Genetic file for inspection with ID {id} not found." });
            }

            var (data, fileName) = result.Value;
            return Results.File(data, "application/octet-stream", fileName);
        }

        private static async Task<IResult> DeleteGeneticFile(IGeneticInspectionService service, int id)
        {
            var deleted = await service.DeleteGeneticFileAsync(id);

            return deleted
                ? Results.NoContent()
                : Results.NotFound(new { Message = $"Genetic file for inspection with ID {id} not found." });
        }

        private static async Task<IResult> GetQpadmResult(IGeneticInspectionService service, int id)
        {
            var response = await service.GetQpadmResultAsync(id);

            return response is null
                ? Results.NotFound(new { Message = $"No QPADM result found for inspection with ID {id}." })
                : Results.Ok(response);
        }

        private static async Task<IResult> SubmitQpadmResult(
            IGeneticInspectionService service,
            int id,
            [FromBody] SubmitQpadmResultContract.Request request)
        {
            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            var response = await service.SubmitQpadmResultAsync(id, request);

            return response is null
                ? Results.NotFound(new { Message = $"Genetic inspection with ID {id} not found." })
                : Results.Created($"/api/genetic-inspections/{id}/qpadm-result", response);
        }

        private static async Task<IResult> SubmitVahaduoResult(
            IGeneticInspectionService service,
            int id,
            [FromBody] SubmitVahaduoResultContract.Request request)
        {
            var response = await service.SubmitVahaduoResultAsync(id, request);

            return response is null
                ? Results.NotFound(new { Message = $"Genetic inspection with ID {id} not found." })
                : Results.Created($"/api/genetic-inspections/{id}/vahaduo-result", response);
        }
    }
}
