using System.Security.Claims;
using Odin.Api.Endpoints.RawGeneticFileManagement.Models;
using Odin.Api.Extensions;

namespace Odin.Api.Endpoints.RawGeneticFileManagement
{
    public static class RawGeneticFileEndpoints
    {
        public static void MapRawGeneticFileEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/raw-genetic-files");

            endpoints.MapGet("/", GetAllFiles)
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("authenticated");
            
            endpoints.MapGet("/{id:int}", GetFileById)
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("authenticated");
            
            endpoints.MapGet("/{id:int}/download", DownloadFile)
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("authenticated");
            
            endpoints.MapPost("/", UploadFile)
                .DisableAntiforgery()
                .RequireAuthorization("ScientistOrAdmin")
                .RequireRateLimiting("file-upload")
                .WithRequestTimeout(TimeSpan.FromMinutes(5));
            
            endpoints.MapDelete("/{id:int}", DeleteFile)
                .RequireAuthorization("EmailVerified")
                .RequireRateLimiting("authenticated");
        }

        private static async Task<IResult> GetAllFiles(IRawGeneticFileService service, HttpContext httpContext)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            var files = await service.GetAllFilesAsync(identityId);
            return Results.Ok(files);
        }

        private static async Task<IResult> GetFileById(IRawGeneticFileService service, HttpContext httpContext, int id)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            var file = await service.GetFileByIdAsync(id, identityId);

            return file is null
                ? Results.NotFound(new { Message = $"File with ID {id} not found." })
                : Results.Ok(file);
        }

        private static async Task<IResult> DownloadFile(IRawGeneticFileService service, HttpContext httpContext, int id)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            var (data, fileName, statusCode) = await service.DownloadFileAsync(id, identityId);

            return statusCode switch
            {
                200 => Results.File(data!, "application/octet-stream", fileName),
                403 => Results.Forbid(),
                _ => Results.NotFound(new { Message = $"File with ID {id} not found." })
            };
        }

        private static async Task<IResult> UploadFile(IRawGeneticFileService service, HttpContext httpContext, IFormFile file)
        {
            var request = new UploadGeneticFileContract.Request { File = file };

            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            var response = await service.UploadFileAsync(request, identityId);
            return Results.Created($"/api/raw-genetic-files/{response.Id}", response);
        }

        private static async Task<IResult> DeleteFile(IRawGeneticFileService service, HttpContext httpContext, int id)
        {
            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub")
                             ?? string.Empty;

            try
            {
                var (deleted, statusCode) = await service.DeleteFileAsync(id, identityId);

                return statusCode switch
                {
                    200 when deleted => Results.NoContent(),
                    403 => Results.Forbid(),
                    _ => Results.NotFound(new { Message = $"File with ID {id} not found." })
                };
            }
            catch (InvalidOperationException ex)
            {
                return Results.BadRequest(new { Message = ex.Message });
            }
        }
    }
}
