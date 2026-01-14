using Microsoft.AspNetCore.Mvc;
using Odin.Api.Endpoints.GeneticInspectionManagement.Models;
using Odin.Api.Extensions;

namespace Odin.Api.Endpoints.GeneticInspectionManagement
{
    public static class GeneticInspectionEndpoints
    {
        public static void MapGeneticInspectionEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/genetic-inspections");

            endpoints.MapGet("/", GetAll);
            endpoints.MapGet("/{id:int}", GetById);
            endpoints.MapPost("/", Create);
            endpoints.MapDelete("/{id:int}", Delete);
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
    }
}
