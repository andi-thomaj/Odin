using System.Net;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.CladeFinderManagement.Models;
using Odin.Api.Endpoints.OrderManagement;

namespace Odin.Api.Endpoints.CladeFinderManagement
{
    public interface IYHaplogroupComputeService
    {
        /// <summary>
        /// Computes the Y-DNA clade for a qpAdm genetic inspection from its stored raw data and caches the
        /// outcome in <see cref="QpadmCladeResult"/>. Idempotent: a Completed result is left untouched, and
        /// any other prior outcome is overwritten. Designed to run as a Hangfire background job (the single
        /// int argument is Hangfire-serializable). Rethrows on transient failures so Hangfire retries.
        /// </summary>
        Task ComputeAndPersistAsync(int geneticInspectionId, CancellationToken cancellationToken = default);
    }

    public sealed class YHaplogroupComputeService(
        ApplicationDbContext dbContext,
        ICladeFinderService cladeFinderService,
        IMemoryCache cache,
        ILogger<YHaplogroupComputeService> logger) : IYHaplogroupComputeService
    {
        private const string ResultsVersion = "v1";

        // Cap retries (Hangfire's default is 10, backing off over days). A transient tools-api outage
        // should be retried a few times, then surface as Unavailable rather than retry indefinitely.
        [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public async Task ComputeAndPersistAsync(int geneticInspectionId, CancellationToken cancellationToken = default)
        {
            var inspection = await dbContext.QpadmGeneticInspections
                .Include(gi => gi.RawGeneticFile)
                .Include(gi => gi.QpadmCladeResult)
                .FirstOrDefaultAsync(gi => gi.Id == geneticInspectionId, cancellationToken);

            if (inspection is null)
            {
                logger.LogWarning("Y-DNA compute skipped: genetic inspection {InspectionId} not found.", geneticInspectionId);
                return;
            }

            // Already computed successfully — nothing to do. (Transient/terminal-failure states are re-attempted.)
            if (inspection.QpadmCladeResult?.Status == CladeAnalysisStatus.Completed)
                return;

            // Y-DNA traces the direct paternal line; biological females have no Y chromosome, so skip the
            // service call entirely and record why.
            if (inspection.Gender == Gender.Female)
            {
                await PersistAsync(inspection, r =>
                {
                    Reset(r);
                    r.Status = CladeAnalysisStatus.NotApplicable;
                    r.Message =
                        "Y-DNA follows the direct paternal line and is only present in male kits, " +
                        "so a Y-DNA haplogroup can't be determined for this sample.";
                }, cancellationToken);
                return;
            }

            var rawData = inspection.RawGeneticFile?.RawData;
            if (rawData is null || rawData.Length == 0)
            {
                logger.LogWarning(
                    "Y-DNA compute for inspection {InspectionId}: no raw genetic data available.", geneticInspectionId);
                await PersistAsync(inspection, r =>
                {
                    Reset(r);
                    r.Status = CladeAnalysisStatus.InvalidData;
                    r.Message = "No raw genetic data is available for this order, so Y-DNA could not be analyzed.";
                }, cancellationToken);
                return;
            }

            var fileName = string.IsNullOrWhiteSpace(inspection.RawGeneticFile!.RawDataFileName)
                ? "raw-data.txt"
                : inspection.RawGeneticFile.RawDataFileName;

            try
            {
                AnalyzeCladeContract.Response result;
                // The MemoryStream must stay open until AnalyzeAsync finishes: the proxy reads
                // file.OpenReadStream() while sending, and that read completes within this await.
                await using (var stream = new MemoryStream(rawData, writable: false))
                {
                    var formFile = new FormFile(stream, 0, rawData.Length, "file", fileName)
                    {
                        Headers = new HeaderDictionary(),
                        ContentType = "application/octet-stream",
                    };

                    // build: null → the tools API auto-detects the genome build from the file header.
                    result = await cladeFinderService.AnalyzeAsync(formFile, build: null, cancellationToken);
                }

                await PersistAsync(inspection, r =>
                {
                    Reset(r);
                    r.Status = CladeAnalysisStatus.Completed;
                    r.Message = null;
                    r.Clade = result.Clade;
                    r.Score = result.Score;
                    r.NextPredictionClade = result.NextPrediction?.Clade;
                    r.NextPredictionScore = result.NextPrediction?.Score;
                    r.Lineage = result.Lineage?.ToList() ?? [];
                    r.Downstream = result.Downstream?
                        .Select(d => new CladeDownstreamItem { Clade = d.Clade, Children = d.Children })
                        .ToList() ?? [];
                    r.PositivesUsed = result.PositivesUsed;
                    r.NegativesUsed = result.NegativesUsed;
                    r.YReads = result.YReads;
                    r.SourceFormat = result.SourceFormat;
                    r.EffectiveBuild = result.EffectiveBuild;
                    r.Warning = result.Warning;
                    r.Error = result.Error;
                }, cancellationToken);

                logger.LogInformation(
                    "Y-DNA compute for inspection {InspectionId} completed: clade={Clade}, yReads={YReads}.",
                    geneticInspectionId, result.Clade, result.YReads);
            }
            catch (CladeFinderException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
                // 400 = terminal for this file: either no Y data, or the file/chip can't be used for Y-DNA.
                var isNoY = ex.Detail.Contains("No Y-chromosome SNP", StringComparison.OrdinalIgnoreCase);
                await PersistAsync(inspection, r =>
                {
                    Reset(r);
                    r.Status = isNoY ? CladeAnalysisStatus.NoYData : CladeAnalysisStatus.InvalidData;
                    r.Message = isNoY
                        ? "No Y-chromosome markers were found in the uploaded file. This usually means the " +
                          "export is autosomal-only or incomplete, so a paternal (Y-DNA) haplogroup can't be determined."
                        : ex.Detail;
                }, cancellationToken);

                logger.LogInformation(
                    "Y-DNA compute for inspection {InspectionId}: {Status} ({Detail}).",
                    geneticInspectionId, isNoY ? "NoYData" : "InvalidData", ex.Detail);
            }
            catch (Exception ex)
            {
                // 503 / 5xx / network / timeout / misconfiguration → transient. Record Unavailable so the UI
                // shows a sensible message even before Hangfire retries, then rethrow to trigger the retry.
                await PersistAsync(inspection, r =>
                {
                    Reset(r);
                    r.Status = CladeAnalysisStatus.Unavailable;
                    r.Message = "Your Y-DNA analysis couldn't be completed just yet. It will be retried automatically.";
                }, cancellationToken);

                logger.LogError(ex,
                    "Y-DNA compute for inspection {InspectionId} failed transiently; will retry.", geneticInspectionId);
                throw;
            }
        }

        /// <summary>
        /// Applies the computed outcome to the inspection's cached clade record (creating it if absent) and
        /// saves. If a concurrent job created the row first (unique-FK violation on insert), it reloads the
        /// winning row and re-applies this outcome as an update — unless that row is already Completed, which
        /// is never overwritten — so the job converges in a single execution instead of relying on retries.
        /// </summary>
        private async Task PersistAsync(
            QpadmGeneticInspection inspection, Action<QpadmCladeResult> mutate, CancellationToken cancellationToken)
        {
            try
            {
                ApplyOutcome(inspection, mutate);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException) when (
                inspection.QpadmCladeResult is { } pending && dbContext.Entry(pending).State == EntityState.Added)
            {
                // Lost the insert race against a concurrent job. Drop our pending insert and reload the winner.
                dbContext.Entry(pending).State = EntityState.Detached;
                inspection.QpadmCladeResult = await dbContext.QpadmCladeResults
                    .FirstOrDefaultAsync(c => c.GeneticInspectionId == inspection.Id, cancellationToken);

                if (inspection.QpadmCladeResult is null ||
                    inspection.QpadmCladeResult.Status == CladeAnalysisStatus.Completed)
                    return;

                ApplyOutcome(inspection, mutate);
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            // A clade result was just (re)written for this inspection — drop any cached qpAdm response so
            // the next result view reflects the new Y-DNA outcome rather than a stale prior state. The only
            // early return above (a Completed row winning the insert race) makes no change and is skipped.
            cache.Remove(OrderResultCacheKeys.Qpadm(inspection.OrderId));
        }

        /// <summary>Loads-or-creates the inspection's cached clade record (in memory), applies the outcome
        /// and stamps audit fields. Does not save.</summary>
        private void ApplyOutcome(QpadmGeneticInspection inspection, Action<QpadmCladeResult> mutate)
        {
            var now = DateTime.UtcNow;
            var createdBy = string.IsNullOrWhiteSpace(inspection.CreatedBy) ? "system" : inspection.CreatedBy;

            var record = inspection.QpadmCladeResult;
            if (record is null)
            {
                record = new QpadmCladeResult
                {
                    GeneticInspectionId = inspection.Id,
                    ResultsVersion = ResultsVersion,
                    CreatedBy = createdBy,
                    CreatedAt = now,
                };
                inspection.QpadmCladeResult = record;
                dbContext.QpadmCladeResults.Add(record);
            }

            mutate(record);

            record.ResultsVersion = ResultsVersion;
            record.UpdatedBy = createdBy;
            record.UpdatedAt = now;
        }

        /// <summary>Clears the clade payload so a re-run never leaves stale fields from a prior outcome.</summary>
        private static void Reset(QpadmCladeResult r)
        {
            r.Message = null;
            r.Clade = null;
            r.Score = null;
            r.NextPredictionClade = null;
            r.NextPredictionScore = null;
            r.Lineage = [];
            r.Downstream = [];
            r.PositivesUsed = 0;
            r.NegativesUsed = 0;
            r.YReads = null;
            r.SourceFormat = null;
            r.EffectiveBuild = null;
            r.Warning = null;
            r.Error = null;
        }
    }
}
