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
        /// Admit waiting merges up to the in-flight cap (2). Picks the oldest <c>NotStarted</c> qpAdm raw
        /// files (the logical FIFO queue), marks each <c>Queued</c>, and enqueues <see cref="RunAsync"/> for
        /// it — but only while (Queued + Converting + Merging) &lt; cap. Serialized across the cluster via
        /// <c>[DisableConcurrentExecution]</c> so the count→admit step can't race. Invoked on order creation,
        /// when a merge finishes (to refill freed capacity), and by a recurring safety-net schedule.
        /// </summary>
        Task DispatchPendingMergesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Convert the inspection's raw upload to 23andMe (persisted in the DB), then merge it into the
        /// AADR panel (bundle stored on the tools-api volume). Idempotent: a Ready/Deleted file is left
        /// untouched. Rethrows on transient failures so Hangfire retries; records terminal failures as
        /// <see cref="Data.Enums.MergeStatus.Failed"/>. Enqueued only by the dispatcher (never directly).
        /// </summary>
        Task RunAsync(int geneticInspectionId, CancellationToken cancellationToken = default);

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
