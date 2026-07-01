using Hangfire;

namespace Odin.Api.Endpoints.MergeManagement
{
    /// <summary>
    /// Background jobs for the automated AADR merge, run via Hangfire. <see cref="RunAsync"/> is
    /// enqueued at qpAdm order creation (onto the serialized "merge" queue); <see cref="DeleteAsync"/>
    /// is enqueued when the order is completed. Both take Hangfire-serializable arguments.
    /// </summary>
    /// <remarks>
    /// The <c>[Queue("merge")]</c> attributes below MUST live on this <b>interface</b>, not only on the
    /// concrete <c>MergeJob</c>. Jobs are enqueued via <c>Enqueue&lt;IMergeJob&gt;(svc =&gt; svc.RunAsync(..))</c>,
    /// so Hangfire reads the queue attribute from the <i>interface</i> method it was handed; an attribute on
    /// the concrete method is never seen, and the job silently lands on the multi-worker "default" queue
    /// instead of the dedicated single-worker "merge" queue — which broke the strictly-serialized guarantee
    /// (it let re-fetched merge jobs run concurrently and produced overlapping forges).
    /// </remarks>
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
        /// <remarks>
        /// <c>[DisableConcurrentExecution]</c> MUST live on this <b>interface</b> method, not only on the
        /// concrete <c>MergeJob</c> — same rule as <c>[Queue]</c>/<c>[AutomaticRetry]</c> on <see cref="RunAsync"/>.
        /// Dispatch is enqueued via <c>Enqueue&lt;IMergeJob&gt;(svc =&gt; svc.DispatchPendingMergesAsync(..))</c>
        /// and registered via <c>AddOrUpdate&lt;IMergeJob&gt;(..)</c>, so Hangfire reads the filter off the
        /// <i>interface</i> method; an attribute on the concrete method is silently ignored. Without it the
        /// every-minute recurring dispatch (multi-worker "default" queue) and the per-event dispatches
        /// (order creation, merge completion, requeue, stop) run concurrently and race the count→admit step,
        /// each reading the same stale in-flight count and admitting a candidate — over-admitting past the cap
        /// of 1, which is how several merges ended up "Merging" at once. Keep this on the interface.
        /// </remarks>
        [DisableConcurrentExecution(60)]
        Task DispatchPendingMergesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Convert the inspection's raw upload to 23andMe (persisted in the DB), then merge it into the
        /// AADR panel (bundle stored on the tools-api volume). Idempotent: a Ready/Deleted file is left
        /// untouched. <b>No automatic retries</b> — any failure (bad upload, panel unavailable, tool error, timeout)
        /// is recorded as <see cref="Data.Enums.MergeStatus.Failed"/>; an admin re-runs it via
        /// <see cref="RequeueAsync"/>. Enqueued only by the dispatcher (never directly).
        /// </summary>
        /// <remarks><c>[AutomaticRetry(Attempts = 0)]</c> is here (not on the concrete method) for the same
        /// reason as <c>[Queue]</c> — Hangfire reads filter attributes off the enqueued interface method. On
        /// the concrete method it was ignored, so a hard-throwing merge would have taken Hangfire's default
        /// 10 retries instead of failing fast.</remarks>
        [Queue("merge")]
        [AutomaticRetry(Attempts = 0)]
        Task RunAsync(int geneticInspectionId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Admin-initiated retry of a non-running merge (typically <c>Failed</c>): resets the raw file to
        /// <c>NotStarted</c> and triggers a dispatch so it re-enters the serialized queue. Throws
        /// <see cref="KeyNotFoundException"/> if the file doesn't exist, or <see cref="InvalidOperationException"/>
        /// if a merge is already in progress or already complete. With no automatic retries, this is the
        /// only way a failed merge is re-attempted.
        /// </summary>
        Task RequeueAsync(int rawGeneticFileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stop an <b>in-progress</b> merge (<c>Queued</c>/<c>Converting</c>/<c>Merging</c>): delete its
        /// Hangfire job (so the running <see cref="RunAsync"/> is cancelled and never re-run), kill the
        /// tools-api tool subprocess + drop any partial bundle, and mark the file <c>Failed</c> so an admin
        /// can re-run it via <see cref="RequeueAsync"/>. Throws <see cref="KeyNotFoundException"/> if the file
        /// is missing, or <see cref="InvalidOperationException"/> (→409) if no merge is in progress.
        /// </summary>
        Task StopAsync(int rawGeneticFileId, CancellationToken cancellationToken = default);

        /// <summary>Delete a completed order's merge bundle from the tools-api volume and mark it Deleted.</summary>
        Task DeleteAsync(int rawGeneticFileId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Delete <b>every</b> still-<c>Ready</c> merge bundle on the tools-api volume right now,
        /// regardless of order status or age — the admin "free all disk" action surfaced on the Input
        /// results grid. Unlike <see cref="CleanupOrphansAsync"/> (which only reclaims completed/expired
        /// orders), this is unconditional. Each delete is idempotent and isolated, so one stuck bundle
        /// doesn't abort the rest. Returns the number of bundles deleted. Runs synchronously (the caller
        /// is an HTTP request that reports the count), not on the Hangfire merge queue.
        /// </summary>
        Task<int> DeleteAllReadyMergedDataAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Safety net (recurring): delete any still-Ready merge bundle whose order is already Completed, or
        /// that is older than the retention window — covering deletes that failed to enqueue or never fired.
        /// Bounded disk is the whole point on the 80 GB host (see the plan's Part C).
        /// </summary>
        [Queue("merge")]
        Task CleanupOrphansAsync(CancellationToken cancellationToken = default);
    }
}
