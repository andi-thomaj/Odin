using System.Net;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;

namespace Odin.Api.Endpoints.MergeManagement
{
    public sealed class MergeJob(
        ApplicationDbContext dbContext,
        IMergePipelineService mergeService,
        IBackgroundJobClient backgroundJobClient,
        TimeProvider timeProvider,
        ILogger<MergeJob> logger) : IMergeJob
    {
        // Hard cap on merge jobs in flight (Queued + Converting + Merging). Matches the merge queue's
        // WorkerCount, so admitted jobs run immediately with no Hangfire backlog; orders beyond the cap
        // wait as NotStarted in the DB. See the plan / README ops notes.
        private const int MaxInFlight = 2;

        // Serialized so the count→admit step can't over-admit when fired from several sources at once
        // (order creation, merge completion, the recurring safety net).
        [DisableConcurrentExecution(60)]
        public async Task DispatchPendingMergesAsync(CancellationToken cancellationToken = default)
        {
            // Retrying counts as in-flight: such a job is still scheduled in Hangfire and occupies a
            // merge slot, so it must be counted or the dispatcher would over-admit and build a backlog.
            var inFlight = await dbContext.RawGeneticFiles
                .CountAsync(f => f.MergeStatus == MergeStatus.Queued
                    || f.MergeStatus == MergeStatus.Converting
                    || f.MergeStatus == MergeStatus.Merging
                    || f.MergeStatus == MergeStatus.Retrying, cancellationToken);

            var capacity = MaxInFlight - inFlight;
            if (capacity <= 0)
                return;

            // Oldest-first across the waiting (NotStarted) qpAdm raw files — one inspection per file.
            var candidates = await dbContext.QpadmGeneticInspections
                .Where(gi => gi.RawGeneticFile != null && gi.RawGeneticFile.MergeStatus == MergeStatus.NotStarted)
                .GroupBy(gi => gi.RawGeneticFileId)
                .Select(g => new { FileId = g.Key, InspectionId = g.Min(x => x.Id) })
                .OrderBy(x => x.FileId)
                .Take(capacity)
                .ToListAsync(cancellationToken);

            foreach (var candidate in candidates)
            {
                var file = await dbContext.RawGeneticFiles
                    .FirstOrDefaultAsync(f => f.Id == candidate.FileId, cancellationToken);
                if (file is null || file.MergeStatus != MergeStatus.NotStarted)
                    continue; // re-check under the lock — another dispatch may have just taken it

                file.MergeStatus = MergeStatus.Queued;
                await dbContext.SaveChangesAsync(cancellationToken);

                backgroundJobClient.Enqueue<IMergeJob>(svc => svc.RunAsync(candidate.InspectionId, CancellationToken.None));
            }

            if (candidates.Count > 0)
                logger.LogInformation("Merge dispatcher admitted {Count} job(s) (was {InFlight}/{Cap} in flight).",
                    candidates.Count, inFlight, MaxInFlight);
        }

        // Serialized onto the low-concurrency "merge" queue (memory-heavy; see Program.cs).
        // Cap retries (Hangfire's default is 10, with backoff stretching to days): a merge that keeps
        // failing transiently — e.g. a 503 because the AADR panel isn't provisioned — should give up
        // after a few attempts and surface as Failed, not retry for days while holding a merge worker.
        [Queue("merge")]
        [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
        public async Task RunAsync(int geneticInspectionId, CancellationToken cancellationToken = default)
        {
            try
            {
                await RunCoreAsync(geneticInspectionId, cancellationToken);
            }
            finally
            {
                // This job just left the in-flight set (success, terminal fail, or about to rethrow for a
                // retry) — a slot may have freed, so admit the next waiting merge. Idempotent + capped.
                backgroundJobClient.Enqueue<IMergeJob>(svc => svc.DispatchPendingMergesAsync(CancellationToken.None));
            }
        }

        private async Task RunCoreAsync(int geneticInspectionId, CancellationToken cancellationToken)
        {
            var inspection = await dbContext.QpadmGeneticInspections
                .Include(gi => gi.RawGeneticFile)
                .FirstOrDefaultAsync(gi => gi.Id == geneticInspectionId, cancellationToken);

            var file = inspection?.RawGeneticFile;
            if (inspection is null || file is null)
            {
                logger.LogWarning("Merge skipped: genetic inspection {InspectionId} or its raw file not found.",
                    geneticInspectionId);
                return;
            }

            // Idempotent: don't redo a finished merge, and never resurrect one deleted after completion.
            if (file.MergeStatus is MergeStatus.Ready or MergeStatus.Deleted)
                return;

            if (file.RawData is null || file.RawData.Length == 0)
            {
                await SetStatusAsync(file, MergeStatus.Failed,
                    "No raw genetic data is available for this order.", cancellationToken);
                return;
            }

            try
            {
                // Time the whole convert+merge so the table can show how long the order took to merge.
                var mergeStopwatch = System.Diagnostics.Stopwatch.StartNew();

                // 1. Convert raw → 23andMe and persist (small, kept in the DB).
                file.MergeStatus = MergeStatus.Converting;
                await dbContext.SaveChangesAsync(cancellationToken);

                var rawFileName = string.IsNullOrWhiteSpace(file.RawDataFileName) ? "raw-data.txt" : file.RawDataFileName;
                var converted = await mergeService.ConvertAsync(file.RawData, rawFileName, cancellationToken);

                file.Converted23AndMeData = System.Text.Encoding.UTF8.GetBytes(converted.Converted23Andme);
                file.Converted23AndMeFileName = converted.FileName;

                // 2. Merge into the AADR panel (heavy). The bundle lands on the tools-api volume.
                file.MergeStatus = MergeStatus.Merging;
                var mergeId = $"insp-{geneticInspectionId}-{Guid.NewGuid():N}";
                file.MergeId = mergeId;
                await dbContext.SaveChangesAsync(cancellationToken);

                var result = await mergeService.RunMergeAsync(
                    mergeId,
                    converted.Converted23Andme,
                    panel: null, // tools-api default (HO)
                    sampleId: $"S{geneticInspectionId}",
                    sex: SexCode(inspection.Gender),
                    cancellationToken);

                file.MergeStatus = MergeStatus.Ready;
                file.MergeFileName = result.FileName;
                file.MergeSizeBytes = result.SizeBytes;
                file.MergeDurationSeconds = mergeStopwatch.Elapsed.TotalSeconds;
                file.MergeError = null;
                await dbContext.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Merge for inspection {InspectionId} ready: id={MergeId}, {SizeBytes} bytes, panel={Panel}.",
                    geneticInspectionId, result.MergeId, result.SizeBytes, result.Panel);
            }
            catch (MergePipelineException ex) when (ex.StatusCode == HttpStatusCode.BadRequest)
            {
                // Terminal: the upload can't be converted/used. Retrying the same bytes won't help.
                await SetStatusAsync(file, MergeStatus.Failed, ex.Detail, cancellationToken);
                logger.LogWarning("Merge for inspection {InspectionId} failed (terminal): {Detail}.",
                    geneticInspectionId, ex.Detail);
            }
            catch (Exception ex)
            {
                // 503 (AADR not provisioned) / 5xx / timeout / network → transient. Mark Retrying (not
                // Failed) so the order isn't shown as failed mid-retry and the dispatcher keeps counting
                // it as in-flight; then rethrow so Hangfire retries. If retries are exhausted, the job
                // lands in Hangfire's FailedState and MergeJobFailureStateFilter reconciles it to Failed.
                await SetStatusAsync(file, MergeStatus.Retrying, Truncate(ex.Message, 1000), cancellationToken);
                logger.LogError(ex, "Merge for inspection {InspectionId} failed transiently; will retry.",
                    geneticInspectionId);
                throw;
            }
        }

        public async Task DeleteAsync(int rawGeneticFileId, CancellationToken cancellationToken = default)
        {
            var file = await dbContext.RawGeneticFiles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == rawGeneticFileId, cancellationToken);

            if (file is null || string.IsNullOrWhiteSpace(file.MergeId))
                return;
            if (file.MergeStatus == MergeStatus.Deleted)
                return;

            await mergeService.DeleteAsync(file.MergeId, cancellationToken);

            file.MergeStatus = MergeStatus.Deleted;
            file.MergeFileName = null;
            file.MergeSizeBytes = null;
            await dbContext.SaveChangesAsync(cancellationToken);

            logger.LogInformation("Deleted merge bundle {MergeId} for raw file {FileId} after order completion.",
                file.MergeId, rawGeneticFileId);
        }

        // How long an unconsumed (order-not-completed) merge bundle may linger before the cleanup
        // sweep reclaims its disk. With qpAdm automation a bundle is normally consumed (order Completed,
        // inline-deleted) within minutes; this is only a backstop for stalled/never-completed orders.
        private const int RetentionHours = 24;

        [Queue("merge")]
        public async Task CleanupOrphansAsync(CancellationToken cancellationToken = default)
        {
            var cutoff = timeProvider.GetUtcNow().UtcDateTime.AddHours(-RetentionHours);

            var orphanIds = await dbContext.RawGeneticFiles
                .IgnoreQueryFilters()
                .Where(f => f.MergeStatus == MergeStatus.Ready && f.MergeId != null)
                .Where(f => f.UpdatedAt < cutoff
                    || f.GeneticInspections.Any(gi => gi.Order.Status == OrderStatus.Completed))
                .Select(f => f.Id)
                .ToListAsync(cancellationToken);

            if (orphanIds.Count == 0)
                return;

            var deleted = 0;
            foreach (var id in orphanIds)
            {
                try
                {
                    await DeleteAsync(id, cancellationToken);
                    deleted++;
                }
                catch (Exception ex)
                {
                    // Don't let one stuck bundle abort the sweep; the next run retries it.
                    logger.LogError(ex, "Merge orphan cleanup: failed to delete bundle for raw file {FileId}.", id);
                }
            }

            logger.LogInformation("Merge orphan cleanup: deleted {Deleted}/{Total} bundle(s).", deleted, orphanIds.Count);
        }

        private async Task SetStatusAsync(
            RawGeneticFile file, MergeStatus status, string? error, CancellationToken cancellationToken)
        {
            file.MergeStatus = status;
            file.MergeError = error is null ? null : Truncate(error, 1000);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        private static string SexCode(Gender? gender) => gender switch
        {
            Gender.Male => "1",
            Gender.Female => "2",
            _ => "0",
        };

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value[..max];
    }
}
