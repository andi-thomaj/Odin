using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Odin.Api.Endpoints.GeneticInspectionManagement.Models;
using Odin.Api.Endpoints.MergeManagement;
using Odin.Api.Endpoints.RawGeneticFileManagement.Models;
using Odin.Api.Extensions;

namespace Odin.Api.Endpoints.GeneticInspectionManagement
{
    public static class GeneticInspectionEndpoints
    {
        public static void MapGeneticInspectionEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/genetic-inspections");

            endpoints.MapGet("/", GetAll)
                .RequireAuthorization("ScientistOrAdmin")
                .RequireRateLimiting("authenticated")
                .Produces<IEnumerable<GetGeneticInspectionContract.Response>>(StatusCodes.Status200OK);

            endpoints.MapGet("/{id:int}", GetById)
                .RequireAuthorization("ScientistOrAdmin")
                .RequireRateLimiting("authenticated")
                .Produces<GetGeneticInspectionContract.Response>(StatusCodes.Status200OK);

            endpoints.MapPost("/", Create)
                .RequireAuthorization("ScientistOrAdmin")
                .RequireRateLimiting("authenticated")
                .Produces<CreateGeneticInspectionContract.Response>(StatusCodes.Status201Created);

            endpoints.MapDelete("/{id:int}", Delete)
                .RequireAuthorization("AdminOnly")
                .RequireRateLimiting("strict")
                .Produces(StatusCodes.Status204NoContent);

            endpoints.MapPost("/{id:int}/genetic-file", UploadGeneticFile)
                .DisableAntiforgery()
                .RequireAuthorization("ScientistOrAdmin")
                .RequireRateLimiting("file-upload")
                .Produces<UploadGeneticFileContract.Response>(StatusCodes.Status201Created)
                .WithRequestTimeout(TimeSpan.FromMinutes(5));

            endpoints.MapGet("/{id:int}/genetic-file/download", DownloadGeneticFile)
                .RequireAuthorization("ScientistOrAdmin")
                .RequireRateLimiting("authenticated");

            // Download the merged AADR product (the .geno/.snp/.ind triplet) as a .zip. The tools-api
            // stores it as a .tar.gz, so we re-package it to .zip on the fly (streamed, no buffering).
            endpoints.MapGet("/{id:int}/merged-data/download", DownloadMergedData)
                .RequireAuthorization("ScientistOrAdmin")
                .RequireRateLimiting("authenticated");

            endpoints.MapDelete("/{id:int}/genetic-file", DeleteGeneticFile)
                .RequireAuthorization("ScientistOrAdmin")
                .RequireRateLimiting("authenticated")
                .Produces(StatusCodes.Status204NoContent);

            endpoints.MapGet("/{id:int}/qpadm-result", GetQpadmResult)
                .RequireAuthorization("ScientistOrAdmin")
                .RequireRateLimiting("authenticated")
                .Produces<SubmitQpadmResultContract.Response>(StatusCodes.Status200OK);

            endpoints.MapPost("/{id:int}/qpadm-result", SubmitQpadmResult)
                .DisableAntiforgery()
                .RequireAuthorization("ScientistOrAdmin")
                .RequireRateLimiting("file-upload")
                .Produces<SubmitQpadmResultContract.Response>(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status409Conflict) // AADR merge not finished — results can't be submitted yet
                .WithRequestTimeout(TimeSpan.FromMinutes(5));
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
            HttpContext httpContext,
            [FromBody] CreateGeneticInspectionContract.Request request)
        {
            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            var identityId = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                             ?? httpContext.User.FindFirstValue("sub");
            if (string.IsNullOrEmpty(identityId))
                return Results.Unauthorized();

            var response = await service.CreateAsync(request, identityId);
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

        private static async Task<IResult> DownloadMergedData(
            IGeneticInspectionService service, IMergePipelineService mergeService, HttpContext httpContext, int id)
        {
            var (statusCode, error, mergeId, fileName) = await service.ResolveMergedDataDownloadAsync(id);
            if (statusCode != StatusCodes.Status200OK)
            {
                return statusCode switch
                {
                    StatusCodes.Status409Conflict => Results.Conflict(new { Message = error }),
                    _ => Results.NotFound(new { Message = error })
                };
            }

            // Pull the bundle from the tools-api (it's a .tar.gz) without buffering, then re-package it to
            // a .zip on the wire so the client gets a folder that opens natively on Windows/macOS.
            var ct = httpContext.RequestAborted;
            HttpResponseMessage upstream;
            try
            {
                upstream = await mergeService.OpenDownloadAsync(mergeId!, ct);
            }
            catch (MergePipelineException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return Results.NotFound(new { Message = "The merged dataset is no longer available for this order." });
            }

            // ZipArchive has only a synchronous write API (and flushes the central directory on dispose),
            // but Kestrel forbids synchronous response I/O by default — so the stream aborts at the end
            // with ERR_HTTP2_PROTOCOL_ERROR. Opt this one streamed-zip response into synchronous I/O.
            httpContext.Features.Get<IHttpBodyControlFeature>()!.AllowSynchronousIO = true;

            return Results.Stream(async outputStream =>
            {
                using (upstream)
                await using (var body = await upstream.Content.ReadAsStreamAsync(ct))
                await using (var gunzip = new GZipStream(body, CompressionMode.Decompress))
                using (var tar = new TarReader(gunzip))
                using (var zip = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    // The bundle holds the 3 merged files (<sample>_merged.geno/.snp/.ind). Copy each into
                    // the zip; DataStream is valid only until the next entry, so we copy before advancing.
                    while (await tar.GetNextEntryAsync(cancellationToken: ct) is { } entry)
                    {
                        if (entry.DataStream is null
                            || entry.EntryType is not (TarEntryType.RegularFile or TarEntryType.V7RegularFile))
                            continue;

                        // .geno is large + packed binary (poorly compressible) — Fastest avoids burning CPU
                        // re-deflating it for marginal size gain over the panel's already-compact data.
                        var zipEntry = zip.CreateEntry(Path.GetFileName(entry.Name), CompressionLevel.Fastest);
                        await using var entryStream = zipEntry.Open();
                        await entry.DataStream.CopyToAsync(entryStream, ct);
                    }
                }
            }, "application/zip", fileName);
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
            [FromForm] SubmitQpadmResultContract.Request request)
        {
            var validationProblem = request.ValidateAndGetProblem();
            if (validationProblem is not null)
            {
                return validationProblem;
            }

            var (response, statusCode, error) = await service.SubmitQpadmResultAsync(id, request);

            return statusCode switch
            {
                StatusCodes.Status201Created =>
                    Results.Created($"/api/genetic-inspections/{id}/qpadm-result", response),
                StatusCodes.Status409Conflict => Results.Conflict(new { Message = error }),
                _ => Results.NotFound(new { Message = error ?? $"Genetic inspection with ID {id} not found." })
            };
        }

    }
}
