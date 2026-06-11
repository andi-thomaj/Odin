namespace Odin.Api.Data.Enums
{
    /// <summary>
    /// Lifecycle of the automated AADR merge for a qpAdm order. The user's raw upload is converted
    /// to 23andMe (stored in the DB) and then merged into an AADR panel; the multi-GB merge bundle
    /// lives on the odin-tools-api filesystem, tracked here by <c>MergeId</c>, and is deleted once
    /// the order is Completed.
    /// </summary>
    public enum MergeStatus
    {
        /// <summary>
        /// No merge has been attempted yet. This is also the "waiting in the logical queue" state:
        /// a just-created order sits here until the dispatcher admits it (in-flight &lt; cap). Default
        /// for legacy / just-created rows.
        /// </summary>
        NotStarted,

        /// <summary>Admitted by the dispatcher and enqueued in Hangfire, not yet picked up by a worker.</summary>
        Queued,

        /// <summary>The raw upload is being normalized to 23andMe format.</summary>
        Converting,

        /// <summary>The 23andMe file is being merged into the AADR panel (heavy; minutes).</summary>
        Merging,

        /// <summary>The merge bundle is available on the tools-api volume and downloadable.</summary>
        Ready,

        /// <summary>Conversion or merge failed terminally (a bad upload), or all retries were exhausted.</summary>
        Failed,

        /// <summary>
        /// A transient conversion/merge failure occurred and Hangfire will retry. Distinct from
        /// <see cref="Failed"/> so (a) the order isn't shown as failed mid-retry, and (b) the dispatcher
        /// still counts it as in-flight — it occupies a merge slot until it succeeds, fails terminally,
        /// or exhausts retries. On retry exhaustion a Hangfire state filter reconciles it to Failed.
        /// Stored as a string (HasConversion&lt;string&gt;), so adding this value needs no data migration.
        /// </summary>
        Retrying,

        /// <summary>The bundle has been deleted after the order was completed.</summary>
        Deleted
    }
}
