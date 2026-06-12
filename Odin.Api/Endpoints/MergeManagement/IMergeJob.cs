namespace Odin.Api.Endpoints.MergeManagement
{
    /// <summary>
    /// Background jobs for the automated AADR merge, run via Hangfire. <see cref="RunAsync"/> is
    /// enqueued at qpAdm order creation (onto the serialized "merge" queue); <see cref="DeleteAsync"/>
    /// is enqueued when the order is completed. Both take Hangfire-serializable arguments.
    /// </summary>
    public interface IMergeJob
    {
        /// <summary>
        /// Admit waiting merges up to the in-flight cap (pinned to 1 — merges run strictly sequentially). Picks
        /// the oldest <c>NotStarted</c> qpAdm raw files (the logical FIFO queue), marks each <c>Queued</c>,
        /// and enqueues <see cref="RunAsync"/> for it — but only while (Queued + Converting + Merging) &lt;
        /// cap. Serialized across the cluster via <c>[DisableConcurrentExecution]</c> so the count→admit step
        /// can't race. Invoked on order creation, when a merge finishes (to refill freed capacity), and by a
        /// recurring safety-net schedule.
        /// </summary>
        Task DispatchPendingMergesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Convert the inspection's raw upload to 23andMe (persisted in the DB), then merge it into the
        /// AADR panel (bundle stored on the tools-api volume). Idempotent: a Ready/Deleted file is left
        /// untouched. <b>No automatic retries</b> — any failure (bad upload, panel unavailable, tool error, timeout)
        /// is recorded as <see cref="Data.Enums.MergeStatus.Failed"/>; an admin re-runs it via
        /// <see cref="RequeueAsync"/>. Enqueued only by the dispatcher (never directly).
        /// </summary>
        Task RunAsync(int geneticInspectionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Admin-initiated retry of a non-running merge (typically <c>Failed</c>): resets the raw file to
        /// <c>NotStarted</c> and triggers a dispatch so it re-enters the serialized queue. Throws
        /// <see cref="KeyNotFoundException"/> if the file doesn't exist, or <see cref="InvalidOperationException"/>
        /// if a merge is already in progress or already complete. With no automatic retries, this is the
        /// only way a failed merge is re-attempted.
        /// </summary>
        Task RequeueAsync(int rawGeneticFileId, CancellationToken cancellationToken = default);

        /// <summary>Delete a completed order's merge bundle from the tools-api volume and mark it Deleted.</summary>
        Task DeleteAsync(int rawGeneticFileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Safety net (recurring): delete any still-Ready merge bundle whose order is already Completed, or
        /// that is older than the retention window — covering deletes that failed to enqueue or never fired.
        /// Bounded disk is the whole point on the 80 GB host (see the plan's Part C).
        /// </summary>
        Task CleanupOrphansAsync(CancellationToken cancellationToken = default);
    }
}
