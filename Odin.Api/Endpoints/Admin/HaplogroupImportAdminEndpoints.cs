using System.Security.Claims;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.HaplogroupHeatmap;
using Odin.Api.Endpoints.HaplogroupHeatmap.Models;

namespace Odin.Api.Endpoints.Admin
{
    /// <summary>
    /// Admin trigger + status for the rerunnable Y-haplogroup heatmap import (AADR + YFull → Postgres).
    /// The import itself runs as a Hangfire job (<see cref="IHaplogroupImportService"/>); these endpoints
    /// only enqueue it and report the last run, so the long-running load never blocks the request.
    /// Surfaced in the frontend under Reference Data → QpAdm.
    /// </summary>
    public static class HaplogroupImportAdminEndpoints
    {
        public static void MapHaplogroupImportAdminEndpoints(this IEndpointRouteBuilder app)
        {
            var endpoints = app.MapGroup("api/admin/haplogroup-import")
                .RequireAuthorization("AdminOnly");

            endpoints.MapPost("/start", StartImport)
                .Produces<HaplogroupImportContract.StartResponse>(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status409Conflict);

            endpoints.MapGet("/status", GetStatus)
                .Produces<HaplogroupImportContract.StatusResponse>(StatusCodes.Status200OK);
        }

        private static async Task<IResult> StartImport(
            ApplicationDbContext dbContext,
            IBackgroundJobClient jobClient,
            ClaimsPrincipal user,
            CancellationToken cancellationToken)
        {
            // Don't pile up runs: if one is already in progress, surface a 409 rather than enqueue a second
            // (the job also guards itself with DisableConcurrentExecution).
            var running = await dbContext.HaplogroupImportRuns
                .AnyAsync(r => r.Status == HaplogroupImportStatus.Running, cancellationToken);
            if (running)
            {
                return Results.Conflict(new { message = "A haplogroup import is already running." });
            }

            var triggeredBy = user.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? user.FindFirstValue("sub") ?? "admin";
            var jobId = jobClient.Enqueue<IHaplogroupImportService>(s => s.ImportAsync(triggeredBy, CancellationToken.None));

            return Results.Ok(new HaplogroupImportContract.StartResponse { Enqueued = true, JobId = jobId });
        }

        private static async Task<IResult> GetStatus(
            ApplicationDbContext dbContext, CancellationToken cancellationToken)
        {
            var latest = await dbContext.HaplogroupImportRuns
                .OrderByDescending(r => r.Id)
                .Select(r => new HaplogroupImportContract.RunDto
                {
                    Id = r.Id,
                    StartedAt = r.StartedAt,
                    CompletedAt = r.CompletedAt,
                    Status = r.Status,
                    DatasetVersion = r.DatasetVersion,
                    SampleCount = r.SampleCount,
                    NodeCount = r.NodeCount,
                    UnresolvedCount = r.UnresolvedCount,
                    Error = r.Error,
                    TriggeredBy = r.TriggeredBy,
                })
                .FirstOrDefaultAsync(cancellationToken);

            return Results.Ok(new HaplogroupImportContract.StatusResponse
            {
                Latest = latest,
                IsRunning = latest?.Status == HaplogroupImportStatus.Running,
            });
        }
    }
}
