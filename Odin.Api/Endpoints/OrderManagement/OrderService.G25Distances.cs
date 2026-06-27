using System.Diagnostics;
using System.Text;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.Admin.Models;
using Odin.Api.Endpoints.CladeFinderManagement;
using Odin.Api.Endpoints.CladeFinderManagement.Models;
using Odin.Api.Endpoints.G25Calculations;
using Odin.Api.Endpoints.G25Calculations.Models;
using Odin.Api.Endpoints.MergeManagement;
using Odin.Api.Configuration;
using Odin.Api.Endpoints.OrderManagement.Models;
using Odin.Api.Extensions;
using Odin.Api.Services;

namespace Odin.Api.Endpoints.OrderManagement;

/// <summary>
/// OrderService (partial): G25 distance recomputation and the admin G25-inspection listing.
/// Split out of OrderService.cs to keep the main file focused on order CRUD + result reads.
/// Same class — primary-constructor params and private members are shared across the partials.
/// </summary>
public partial class OrderService
{
        public async Task<RecomputeG25DistancesContract.Response> RecomputeG25DistanceResultsAsync(string identityId, IReadOnlyList<int>? inspectionIds = null)
        {
            var startedAt = DateTime.UtcNow;
            var stopwatch = Stopwatch.StartNew();
            var runId = $"v{startedAt:yyyyMMddHHmmss}";

            var eras = await dbContext.G25DistanceEras
                .AsNoTracking()
                .Where(e => e.G25DistancePopulationSamples.Any())
                .Select(e => new { e.Id, e.Name })
                .ToListAsync();

            var response = new RecomputeG25DistancesContract.Response
            {
                Version = runId,
                ErasConsidered = eras.Count,
                InspectionsRequested = inspectionIds?.Count ?? 0,
            };

            if (eras.Count == 0)
            {
                logger.LogWarning("G25 distance recompute aborted: no eras with attached population samples.");
                stopwatch.Stop();
                response.DurationMs = stopwatch.ElapsedMilliseconds;
                return response;
            }

            // Recompute is driven by a change to SHARED reference data (G25 distance population samples), so it
            // runs across every inspection that has coordinates.
            var inspectionsQuery = dbContext.G25GeneticInspections
                .Where(gi => gi.G25Coordinates != null && gi.G25Coordinates != "");

            if (inspectionIds is { Count: > 0 })
            {
                var idSet = inspectionIds.ToHashSet();
                inspectionsQuery = inspectionsQuery.Where(gi => idSet.Contains(gi.Id));
            }

            var inspections = await inspectionsQuery
                .Select(gi => new { gi.Id, gi.OrderId, gi.FirstName, gi.LastName, gi.G25Coordinates })
                .ToListAsync();

            if (response.InspectionsRequested == 0)
            {
                response.InspectionsRequested = inspections.Count;
            }

            // Batch-load all existing distance results for the inspections in scope in a single
            // query rather than one round-trip per inspection. Tracked (no AsNoTracking) so the
            // in-place mutations below register with the change tracker.
            var inspectionIdsToLoad = inspections.Select(i => i.Id).ToList();
            var allExisting = await dbContext.G25DistanceResults
                .Where(r => inspectionIdsToLoad.Contains(r.GeneticInspectionId))
                .ToListAsync();
            var existingByInspection = allExisting
                .GroupBy(r => r.GeneticInspectionId)
                .ToDictionary(g => g.Key, g => g.ToDictionary(r => r.G25DistanceEraId));

            // Order IDs whose cached G25 result payload must be busted once the recompute commits.
            var changedOrderIds = new List<int>();

            foreach (var inspection in inspections)
            {
                if (!existingByInspection.TryGetValue(inspection.Id, out var existingByEra))
                    existingByEra = new Dictionary<int, G25DistanceResult>();

                var highestExistingVersion = existingByEra.Values
                    .Select(r => r.ResultsVersion)
                    .OrderByDescending(ParseG25DistanceVersionNumber)
                    .FirstOrDefault();
                var inspectionVersion = NextG25DistanceVersion(highestExistingVersion);

                var targetName = BuildTargetName(inspection.FirstName, inspection.LastName);
                var normalizedTarget = NormalizeCoordinatesForTarget(inspection.G25Coordinates!, targetName);
                var changedForInspection = 0;

                foreach (var era in eras)
                {
                    var (computeResponse, error, notFound) = await g25CalculationService.ComputeDistancesAsync(
                        new ComputeDistancesContract.Request
                        {
                            TargetCoordinates = normalizedTarget,
                            G25DistanceEraId = era.Id,
                            MaxResults = G25DistanceMaxResults
                        });

                    if (computeResponse is null)
                    {
                        logger.LogWarning(
                            "G25 distance recompute failed for inspection {InspectionId}, era {EraId} ({EraName}). NotFound={NotFound}. Error={Error}",
                            inspection.Id, era.Id, era.Name, notFound, error);
                        continue;
                    }

                    if (computeResponse.Results.Count == 0)
                        continue;

                    var populations = computeResponse.Results[0].Rows
                        .Select((row, index) => new G25DistancePopulation
                        {
                            Name = row.Name,
                            Distance = row.Distance,
                            Rank = index + 1
                        })
                        .ToList();

                    if (populations.Count == 0)
                        continue;

                    var now = DateTime.UtcNow;
                    if (existingByEra.TryGetValue(era.Id, out var existing))
                    {
                        existing.Populations = populations;
                        existing.ResultsVersion = inspectionVersion;
                        existing.UpdatedBy = identityId;
                        existing.UpdatedAt = now;
                    }
                    else
                    {
                        dbContext.G25DistanceResults.Add(new G25DistanceResult
                        {
                            GeneticInspectionId = inspection.Id,
                            G25DistanceEraId = era.Id,
                            ResultsVersion = inspectionVersion,
                            Populations = populations,
                            CreatedBy = identityId,
                            CreatedAt = now,
                            UpdatedAt = now,
                        });
                    }

                    changedForInspection++;
                }

                if (changedForInspection > 0)
                {
                    response.InspectionsProcessed++;
                    response.ResultsUpserted += changedForInspection;
                    changedOrderIds.Add(inspection.OrderId);
                }
                else
                {
                    response.InspectionsSkipped++;
                }
            }

            // Single commit at the end: all upserts go in one transaction. A mid-run failure
            // leaves the previous run's results intact, so re-running is safe (the recompute
            // bumps ResultsVersion, so partial writes from a prior attempt don't collide).
            if (response.ResultsUpserted > 0)
            {
                await dbContext.SaveChangesAsync();

                foreach (var changedOrderId in changedOrderIds)
                    cache.Remove(OrderResultCacheKeys.G25(changedOrderId));
            }

            stopwatch.Stop();
            response.DurationMs = stopwatch.ElapsedMilliseconds;
            logger.LogInformation(
                "G25 distance recompute complete. Version={Version}, Inspections processed={Processed}, skipped={Skipped}, Results upserted={Results}, Duration={DurationMs}ms",
                response.Version, response.InspectionsProcessed, response.InspectionsSkipped, response.ResultsUpserted, response.DurationMs);

            return response;
        }

        public async Task<List<AdminG25InspectionContract.ListItem>> GetAdminG25InspectionsAsync()
        {
            // Project the latest-result fields via a single ordered subquery so EF emits one SQL
            // statement instead of two scalar subqueries that each re-sort G25DistanceResults.
            return await dbContext.G25GeneticInspections
                .AsNoTracking()
                .OrderByDescending(gi => gi.Id)
                .Select(gi => new
                {
                    gi.Id,
                    gi.OrderId,
                    gi.FirstName,
                    gi.MiddleName,
                    gi.LastName,
                    UserEmail = gi.User != null ? gi.User.Email : null,
                    HasCoordinates = gi.G25Coordinates != null && gi.G25Coordinates != "",
                    ResultCount = gi.G25DistanceResults.Count(),
                    Latest = gi.G25DistanceResults
                        .OrderByDescending(r => r.UpdatedAt)
                        .Select(r => new { r.ResultsVersion, r.UpdatedAt })
                        .FirstOrDefault(),
                })
                .Select(x => new AdminG25InspectionContract.ListItem
                {
                    Id = x.Id,
                    OrderId = x.OrderId,
                    FirstName = x.FirstName,
                    MiddleName = x.MiddleName,
                    LastName = x.LastName,
                    UserEmail = x.UserEmail,
                    HasCoordinates = x.HasCoordinates,
                    ResultCount = x.ResultCount,
                    LatestResultsVersion = x.Latest != null ? x.Latest.ResultsVersion : null,
                    LatestResultsUpdatedAt = x.Latest != null ? (DateTime?)x.Latest.UpdatedAt : null,
                })
                .ToListAsync();
        }

}
