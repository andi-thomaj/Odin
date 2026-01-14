using Odin.Api.Endpoints.RawGeneticFileManagement.Models;
using Odin.Api.Extensions;

namespace Odin.Api.Endpoints.RawGeneticFileManagement
{
    public static class RawGeneticFileEndpoints
    {
        public static void MapRawGeneticFileEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/raw-genetic-files");

            endpoints.MapGet("/", GetAllFiles);
            endpoints.MapGet("/{id:int}", GetFileById);
            endpoints.MapGet("/{id:int}/download", DownloadFile);
            endpoints.MapPost("/", UploadFile).DisableAntiforgery();
            endpoints.MapDelete("/{id:int}", DeleteFile);
        }

        private static async Task<IResult> GetAllFiles(IRawGeneticFileService service)
        {
            var files = await service.GetAllFilesAsync();
            return Results.Ok(files);
        }

        private static async Task<IResult> GetFileById(IRawGeneticFileService service, int id)
        {
            var file = await service.GetFileByIdAsync(id);

            return file is null
                ? Results.NotFound(new { Message = $"File with ID {id} not found." })
                : Results.Ok(file);
        }

        private static async Task<IResult> DownloadFile(IRawGeneticFileService service, int id)
        {
            var result = await service.DownloadFileAsync(id);

            if (result is null)
            {
                return Results.NotFound(new { Message = $"File with ID {id} not found." });
            }

            var (data, fileName) = result.Value;
            return Results.File(data, "application/octet-stream", fileName);
        }

        private static async Task<IResult> UploadFile(IRawGeneticFileService service, IFormFile file)
        {
            var request = new UploadGeneticFileContract.Request { File = file };

            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            var response = await service.UploadFileAsync(request);
            return Results.Created($"/api/raw-genetic-files/{response.Id}", response);
        }

        private static async Task<IResult> DeleteFile(IRawGeneticFileService service, int id)
        {
            var deleted = await service.DeleteFileAsync(id);

            return deleted
                ? Results.NoContent()
                : Results.NotFound(new { Message = $"File with ID {id} not found." });
        }
    }
}
