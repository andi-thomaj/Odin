using System.Net;
using Hangfire;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Odin.Api.Authentication;
using Odin.Api.Configuration;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Hubs;

namespace Odin.Api.Endpoints.MergeManagement
{
    public sealed class MergeJob(
        ApplicationDbContext dbContext,
        IMergePipelineService mergeService,
        IBackgroundJobClient backgroundJobClient,
        JobStorage jobStorage,
        IGeneticInspectionRealtimeNotifier liveUpdates,
        TimeProvider timeProvider,
        IOptions<MergeJobOptions> mergeOptions,
        ILogger<MergeJob> logger) : IMergeJob
    {
        // Cap on merge jobs in flight (Queued + Converting + Merging). Pinned to 1 — merges run strictly
        // sequentially by deliberate policy (the tools-api merges with trident at ~1.3 GB; serializing
        // is a choice, not a RAM necessity).
        // The value is hardcoded to 1 in Program.cs (not bound from config) and matches the single-worker
        // "merge" Hangfire queue, so the admitted job runs immediately; orders beyond it wait as NotStarted
        // in the DB. (The injected option stays settable so unit tests can exercise the admission math.)
        private readonly int _maxInFlight = Math.Max(1, mergeOptions.Value.MaxConcurrentMerges);

        // Serialized so the count→admit step can't over-admit when fired from several sources at once
        // (order creation, merge completion, the recurring safety net). [DisableConcurrentExecution(60)] is on
        // IMergeJob.DispatchPendingMergesAsync (the interface) — NOT here — because Hangfire reads filter
        // attributes off the enqueued INTERFACE method (jobs come in via Enqueue<IMergeJob>/AddOrUpdate<IMergeJob>);
        // on this concrete method it would be silently ignored, letting concurrent dispatches race the
        // count→admit step and over-admit past the cap (the "several merges Merging at once" bug). Same gotcha
        // as [Queue]/[AutomaticRetry] on RunAsync below.
        public async Task DispatchPendingMergesAsync(CancellationToken cancellationToken = default)
        {
            // The merge worker is a single queue (one job at a time), so the dispatcher counts in-flight and
            // picks candidates with IgnoreQueryFilters() — a background run must see every file, including
            // soft-deleted ones, so its in-flight count is accurate.
            // Retrying counts as in-flight: such a job is still scheduled in Hangfire and occupies a
            // merge slot, so it must be counted or the dispatcher would over-admit and build a backlog.
            var inFlight = await dbContext.RawGeneticFiles
                .IgnoreQueryFilters()
                .CountAsync(f => f.MergeStatus == MergeStatus.Queued
                    || f.MergeStatus == MergeStatus.Converting
                    || f.MergeStatus == MergeStatus.Merging
                    || f.MergeStatus == MergeStatus.Retrying, cancellationToken);

            var capacity = _maxInFlight - inFlight;
            if (capacity <= 0)
                return;

            // Oldest-first across the waiting (NotStarted) qpAdm raw files — one inspection per file, any app.
            var candidates = await dbContext.QpadmGeneticInspections
                .IgnoreQueryFilters()
                .Where(gi => gi.RawGeneticFile != null && !gi.RawGeneticFile.IsDeleted
                    && gi.RawGeneticFile.MergeStatus == MergeStatus.NotStarted)
                .GroupBy(gi => gi.RawGeneticFileId)
                .Select(g => new { FileId = g.Key, InspectionId = g.Min(x => x.Id) })
                .OrderBy(x => x.FileId)
                .Take(capacity)
                .ToListAsync(cancellationToken);

            foreach (var candidate in candidates)
            {
                var file = await dbContext.RawGeneticFiles
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(f => f.Id == candidate.FileId, cancellationToken);
                if (file is null || file.MergeStatus != MergeStatus.NotStarted)
                    continue; // re-check under the lock — another dispatch may have just taken it

                file.MergeStatus = MergeStatus.Queued;
                await dbContext.SaveChangesAsync(cancellationToken);
                await BroadcastMergeStatusAsync(file, cancellationToken);

                backgroundJobClient.Enqueue<IMergeJob>(svc => svc.RunAsync(candidate.InspectionId, CancellationToken.None));
            }

            if (candidates.Count > 0)
                logger.LogInformation("Merge dispatcher admitted {Count} job(s) (was {InFlight}/{Cap} in flight).",
                    candidates.Count, inFlight, _maxInFlight);
        }

        // Serialized onto the single-worker "merge" queue; NO automatic retries. Both behaviours are set by
        // [Queue("merge")] + [AutomaticRetry(Attempts = 0)] on IMergeJob.RunAsync — Hangfire reads filter
        // attributes off the enqueued INTERFACE method, so attributes here on the concrete method would be
        // silently ignored (which is exactly the bug that put merges on the multi-worker "default" queue).
        // Rationale: a failed merge usually fails for a reason an immediate retry won't fix (bad upload,
        // panel not provisioned, timeout) and would just tie up the single merge worker; an admin re-runs it
        // (RequeueAsync) instead. Attempts = 0 also stops Hangfire from re-running a job whose worker died
        // (e.g. a redeploy) — that surfaces as Failed via MergeJobFailureStateFilter instead.
        public async Task RunAsync(int geneticInspectionId, CancellationToken cancellationToken = default)
        {
            try
            {
                await RunCoreAsync(geneticInspectionId, cancellationToken);
            }
            finally
            {
                // This job just left the in-flight set (success or terminal fail) — a slot may have freed,
                // so admit the next waiting merge. Idempotent + capped.
                backgroundJobClient.Enqueue<IMergeJob>(svc => svc.DispatchPendingMergesAsync(CancellationToken.None));
            }
        }

        private async Task RunCoreAsync(int geneticInspectionId, CancellationToken cancellationToken)
        {
            // Background job: no HTTP request set the app context, so load the inspection across all apps
            // (by PK, filters off) and pin the request app to it, so writes + any app-scoped reads below
            // stay aligned with the inspection's app rather than defaulting to ancestrify.
            var inspection = await dbContext.QpadmGeneticInspections
                .IgnoreQueryFilters()
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
                await BroadcastMergeStatusAsync(file, cancellationToken);

                var rawFileName = string.IsNullOrWhiteSpace(file.RawDataFileName) ? "raw-data.txt" : file.RawDataFileName;
                var converted = await mergeService.ConvertAsync(file.RawData, rawFileName, cancellationToken);

                file.Converted23AndMeData = System.Text.Encoding.UTF8.GetBytes(converted.Converted23Andme);
                file.Converted23AndMeFileName = converted.FileName;

                // 2. Merge into the AADR panel (heavy). The bundle lands on the tools-api volume.
                file.MergeStatus = MergeStatus.Merging;
                var mergeId = $"insp-{geneticInspectionId}-{Guid.NewGuid():N}";
                file.MergeId = mergeId;
                await dbContext.SaveChangesAsync(cancellationToken);
                await BroadcastMergeStatusAsync(file, cancellationToken);

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
                await BroadcastMergeStatusAsync(file, cancellationToken);

                logger.LogInformation(
                    "Merge for inspection {InspectionId} ready: id={MergeId}, {SizeBytes} bytes, panel={Panel}.",
                    geneticInspectionId, result.MergeId, result.SizeBytes, result.Panel);
            }
            catch (MergePipelineException ex)
            {
                // No auto-retry: every tools-api failure is terminal here — a bad upload (400), the panel
                // not provisioned (503), a merge tool failure (500), or a timeout.
                // Drop any bundle the tools-api may have produced (or is still producing) BEFORE marking
                // Failed, so a failed merge never leaves a stray .tar.gz filling the volume; then record
                // Failed (an admin re-runs it via RequeueAsync once the cause is addressed).
                await DropAnyProducedBundleAsync(file);
                await SetStatusAsync(file, MergeStatus.Failed, ex.Detail, cancellationToken);
                logger.LogWarning("Merge for inspection {InspectionId} failed ({Status}): {Detail}.",
                    geneticInspectionId, (int)ex.StatusCode, ex.Detail);
            }
            catch (Exception ex)
            {
                // Network/timeout/unexpected — also terminal (no auto-retry). Admin re-runs if appropriate.
                // This is the case that actually leaks disk: a .NET client timeout (or lost response) while
                // trident keeps running on its uncancellable threadpool and writes the bundle afterward.
                // DropAnyProducedBundleAsync KILLS a still-running forge AND deletes any finished bundle.
                await DropAnyProducedBundleAsync(file);
                await SetStatusAsync(file, MergeStatus.Failed, Truncate(ex.Message, 1000), cancellationToken);
                logger.LogError(ex, "Merge for inspection {InspectionId} failed.", geneticInspectionId);
            }
        }

        public async Task RequeueAsync(int rawGeneticFileId, CancellationToken cancellationToken = default)
        {
            var file = await dbContext.RawGeneticFiles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == rawGeneticFileId, cancellationToken);
            if (file is null)
                throw new KeyNotFoundException($"No raw genetic file with id {rawGeneticFileId}.");

            if (file.MergeStatus is MergeStatus.Queued or MergeStatus.Converting or MergeStatus.Merging)
                throw new InvalidOperationException("A merge for this file is already in progress.");
            if (file.MergeStatus is MergeStatus.Ready)
                throw new InvalidOperationException("This merge is already complete.");

            var previous = file.MergeStatus;
            // Put it back in the logical queue (NotStarted); the dispatcher admits it when a slot is free.
            file.MergeStatus = MergeStatus.NotStarted;
            file.MergeError = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            await BroadcastMergeStatusAsync(file, cancellationToken);

            backgroundJobClient.Enqueue<IMergeJob>(svc => svc.DispatchPendingMergesAsync(CancellationToken.None));
            logger.LogInformation("Admin requeued merge for raw file {FileId} (was {Previous}).",
                rawGeneticFileId, previous);
        }

        public async Task StopAsync(int rawGeneticFileId, CancellationToken cancellationToken = default)
        {
            var file = await dbContext.RawGeneticFiles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == rawGeneticFileId, cancellationToken);
            if (file is null)
                throw new KeyNotFoundException($"No raw genetic file with id {rawGeneticFileId}.");
            if (file.MergeStatus is not (MergeStatus.Queued or MergeStatus.Converting or MergeStatus.Merging))
                throw new InvalidOperationException("No merge is in progress for this file.");

            // 1. Delete the Hangfire job running this inspection's merge so RunAsync stops (its cancellation
            //    token fires, aborting the in-flight convert/merge HTTP call) and is never re-run. Without
            //    this, a job stopped mid-Converting would proceed to Merging and overwrite the Failed below.
            var inspectionId = await dbContext.QpadmGeneticInspections
                .IgnoreQueryFilters()
                .Where(gi => gi.RawGeneticFileId == rawGeneticFileId)
                .Select(gi => (int?)gi.Id)
                .MinAsync(cancellationToken);
            if (inspectionId is int insp)
                CancelHangfireMergeJob(insp);

            // 2. Kill the running tool on the tools-api (forge can take a poll cycle to notice the cancelled
            //    token) and drop any bundle a just-finished merge may have written. Best-effort.
            if (!string.IsNullOrWhiteSpace(file.MergeId))
            {
                try
                {
                    await mergeService.CancelMergeAsync(file.MergeId, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Stop merge: tools-api cancel for {MergeId} failed (continuing).", file.MergeId);
                }
            }

            // 3. Terminal state so the row clears and the dispatcher won't re-admit it; an admin can Retry.
            //    (RunAsync's own cancellation catch may also write Failed — idempotent.)
            await SetStatusAsync(file, MergeStatus.Failed, "Merge stopped by an administrator.", cancellationToken);

            // A merge slot just freed — admit the next waiting merge.
            backgroundJobClient.Enqueue<IMergeJob>(svc => svc.DispatchPendingMergesAsync(CancellationToken.None));
            logger.LogInformation("Admin stopped in-progress merge for raw file {FileId} (inspection {InspectionId}).",
                rawGeneticFileId, inspectionId);
        }

        // Find and delete the Hangfire RunAsync job for this inspection. Merges are serialized (1 at a
        // time) on the single-worker "merge" queue, but scan BOTH the processing set (Converting/Merging —
        // RunAsync actively running) and the enqueued set (the brief Queued window before a worker picks
        // it). IBackgroundJobClient.Delete cancels a processing job's token and removes an enqueued one
        // before it runs. ("default" is scanned too, defensively, for any job mis-routed there.)
        private void CancelHangfireMergeJob(int inspectionId)
        {
            static bool Matches(global::Hangfire.Common.Job? job, int inspectionId) =>
                job is not null
                && job.Method.Name == nameof(IMergeJob.RunAsync)
                && job.Args.Count > 0 && job.Args[0] is int id && id == inspectionId;

            // Best-effort: a monitoring-API hiccup must not block the stop. Even if this no-ops, the
            // tools-api kill (step 2) + the Failed write (step 3) + the sliding invisibility timeout
            // (so the job isn't re-fetched) already halt the merge; the only thing lost is cancelling a
            // job still in its brief Converting window before it reaches the killable Merging step.
            try
            {
                var monitor = jobStorage.GetMonitoringApi();
                foreach (var (jobId, dto) in monitor.ProcessingJobs(0, 500))
                    if (Matches(dto.Job, inspectionId))
                        backgroundJobClient.Delete(jobId);

                foreach (var queue in new[] { "merge", "default" })
                    foreach (var (jobId, dto) in monitor.EnqueuedJobs(queue, 0, 500))
                        if (Matches(dto.Job, inspectionId))
                            backgroundJobClient.Delete(jobId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Stop merge: could not scan/cancel the Hangfire job for inspection {InspectionId} (continuing).", inspectionId);
            }
        }

        public Task DeleteAsync(int rawGeneticFileId, CancellationToken cancellationToken = default) =>
            TryDeleteBundleAsync(rawGeneticFileId, cancellationToken);

        /// <summary>
        /// Core bundle delete shared by the per-order delete, the orphan sweep, and the bulk "delete all"
        /// action. Returns <c>true</c> only when it actually removed a live bundle (Ready→Deleted), and
        /// <c>false</c> on a no-op (file gone, no <c>MergeId</c>, or already <c>Deleted</c>) so callers can
        /// count genuine reclaims accurately even if a concurrent path already freed the same row.
        /// </summary>
        private async Task<bool> TryDeleteBundleAsync(int rawGeneticFileId, CancellationToken cancellationToken)
        {
            var file = await dbContext.RawGeneticFiles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == rawGeneticFileId, cancellationToken);

            if (file is null || string.IsNullOrWhiteSpace(file.MergeId))
                return false;
            if (file.MergeStatus == MergeStatus.Deleted)
                return false;

            await mergeService.DeleteAsync(file.MergeId, cancellationToken);

            file.MergeStatus = MergeStatus.Deleted;
            file.MergeFileName = null;
            file.MergeSizeBytes = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            await BroadcastMergeStatusAsync(file, cancellationToken);

            logger.LogInformation("Deleted merge bundle {MergeId} for raw file {FileId}.",
                file.MergeId, rawGeneticFileId);
            return true;
        }

        public async Task<int> DeleteAllReadyMergedDataAsync(CancellationToken cancellationToken = default)
        {
            // Every bundle that still occupies disk is a Ready file with a MergeId — the same set the UI
            // shows a download icon for. Unconditional (no retention/completed filter): this is the manual
            // "free all disk now" action, so we reclaim them all.
            var readyIds = await dbContext.RawGeneticFiles
                .IgnoreQueryFilters()
                .Where(f => f.MergeStatus == MergeStatus.Ready && f.MergeId != null)
                .Select(f => f.Id)
                .ToListAsync(cancellationToken);

            if (readyIds.Count == 0)
                return 0;

            var deleted = 0;
            foreach (var id in readyIds)
            {
                // Stop promptly if the admin's request was aborted, rather than grinding through every
                // remaining id (each would throw on the cancel check and log a spurious failure).
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    // Count only bundles this call actually freed — a concurrent delete (orphan sweep,
                    // order-completion delete, a second admin click) may have already taken some.
                    if (await TryDeleteBundleAsync(id, cancellationToken))
                        deleted++;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // Don't let one stuck bundle abort the whole sweep; surface what we managed to free.
                    logger.LogError(ex, "Delete-all merged data: failed to delete bundle for raw file {FileId}.", id);
                }
            }

            logger.LogInformation("Delete-all merged data: deleted {Deleted}/{Total} Ready bundle(s).",
                deleted, readyIds.Count);
            return deleted;
        }

        // How long an unconsumed (order-not-completed) merge bundle may linger before the cleanup
        // sweep reclaims its disk. With qpAdm automation a bundle is normally consumed (order Completed,
        // inline-deleted) within minutes; this is only a backstop for stalled/never-completed orders.
        private const int RetentionHours = 24;

        // [Queue("merge")] is on IMergeJob.CleanupOrphansAsync (Hangfire reads it off the interface method).
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

            var deleted = 0;
            foreach (var id in orphanIds)
            {
                try
                {
                    if (await TryDeleteBundleAsync(id, cancellationToken))
                        deleted++;
                }
                catch (Exception ex)
                {
                    // Don't let one stuck bundle abort the sweep; the next run retries it.
                    logger.LogError(ex, "Merge orphan cleanup: failed to delete bundle for raw file {FileId}.", id);
                }
            }

            // Backstop for the disk leak DropAnyProducedBundleAsync normally closes inline: a merge can fail
            // with a bundle still on the volume when the .NET worker DIED before its catch ran (redeploy,
            // crash) — MergeJobFailureStateFilter then reconciles the row to Failed, but no cleanup fired, so
            // the tools-api bundle (written by trident on its uncancellable threadpool) is orphaned. Neither
            // the Ready sweep above nor an admin Retry (which mints a NEW merge_id) ever reclaims it. Drop the
            // bundle for every Failed row that still carries a merge_id, keeping the Failed status + error.
            var failedWithBundle = await dbContext.RawGeneticFiles
                .IgnoreQueryFilters()
                .Where(f => f.MergeStatus == MergeStatus.Failed && f.MergeId != null)
                .Select(f => f.Id)
                .ToListAsync(cancellationToken);

            var failedReclaimed = 0;
            foreach (var id in failedWithBundle)
            {
                try
                {
                    if (await TryDropFailedBundleAsync(id, cancellationToken))
                        failedReclaimed++;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Merge orphan cleanup: failed to drop orphaned FAILED bundle for raw file {FileId}.", id);
                }
            }

            logger.LogInformation(
                "Merge orphan cleanup: deleted {Deleted}/{Total} Ready bundle(s), reclaimed {FailedReclaimed}/{FailedTotal} orphaned Failed bundle(s).",
                deleted, orphanIds.Count, failedReclaimed, failedWithBundle.Count);
        }

        /// <summary>
        /// Drop the tools-api bundle for a <c>Failed</c> merge that still carries a <c>MergeId</c> (an orphan
        /// left when the worker died before its inline cleanup ran), then detach the merge id so the sweep
        /// won't revisit it and an admin Retry mints a fresh one. The row stays <c>Failed</c> with its error
        /// message — only the now-deleted bundle reference is cleared. Returns <c>false</c> on a no-op (row
        /// gone, no longer Failed, or merge id already cleared).
        /// </summary>
        private async Task<bool> TryDropFailedBundleAsync(int rawGeneticFileId, CancellationToken cancellationToken)
        {
            var file = await dbContext.RawGeneticFiles
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(f => f.Id == rawGeneticFileId, cancellationToken);
            if (file is null || file.MergeStatus != MergeStatus.Failed || string.IsNullOrWhiteSpace(file.MergeId))
                return false;

            // Idempotent on the tools-api: deletes the bundle if present, no-ops (deleted=false) otherwise —
            // so this is safe even when DropAnyProducedBundleAsync already removed it inline.
            await mergeService.DeleteAsync(file.MergeId, cancellationToken);

            file.MergeId = null;
            file.MergeFileName = null;
            file.MergeSizeBytes = null;
            await dbContext.SaveChangesAsync(cancellationToken);
            await BroadcastMergeStatusAsync(file, cancellationToken);

            logger.LogInformation("Reclaimed orphaned bundle for FAILED raw file {FileId}.", rawGeneticFileId);
            return true;
        }

        // Best-effort cleanup after a merge fails: drop any bundle this merge produced — or kill it if the
        // tools-api is STILL running it (a .NET client timeout doesn't cancel forge's uncancellable
        // threadpool, so it keeps going and would otherwise write an orphaned .tar.gz nothing reclaims).
        // CancelMergeAsync covers both: it SIGKILLs a live forge AND deletes any finished/partial bundle for
        // the id (idempotent when nothing is running). Uses CancellationToken.None so it still runs when the
        // failure WAS a cancellation/timeout (the job's token may already be tripped). Never rethrows — a
        // cleanup hiccup must not mask the original failure; CleanupOrphansAsync is the backstop.
        private async Task DropAnyProducedBundleAsync(RawGeneticFile file)
        {
            if (string.IsNullOrWhiteSpace(file.MergeId))
                return; // failed before the merge step ran → the tools-api wrote nothing to delete
            try
            {
                await mergeService.CancelMergeAsync(file.MergeId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Post-failure cleanup: could not drop bundle {MergeId} (the orphan sweep will retry).",
                    file.MergeId);
            }
        }

        private async Task SetStatusAsync(
            RawGeneticFile file, MergeStatus status, string? error, CancellationToken cancellationToken)
        {
            file.MergeStatus = status;
            file.MergeError = error is null ? null : Truncate(error, 1000);
            await dbContext.SaveChangesAsync(cancellationToken);
            await BroadcastMergeStatusAsync(file, cancellationToken);
        }

        // Push a live "this row changed" signal so the "Clients Ancient Origins Results" table refreshes
        // itself without a manual reload whenever a merge moves (a raw file can back more than one row,
        // so this is a file-level event with no single inspection id).
        private Task BroadcastMergeStatusAsync(RawGeneticFile file, CancellationToken cancellationToken) =>
            liveUpdates.NotifyChangedAsync("MergeStatusChanged", inspectionId: null, cancellationToken);

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
