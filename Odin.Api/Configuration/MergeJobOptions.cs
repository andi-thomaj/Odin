namespace Odin.Api.Configuration;

/// <summary>
/// Tuning for the AADR merge job. The merge is memory-heavy (<c>mergeit</c> on the 2M panel needs
/// ~25 GB RAM), so merges run <b>strictly one at a time, sequentially</b>. This caps the dispatcher's
/// in-flight admissions; the Hangfire "merge" queue is likewise pinned to a single worker.
/// <para>
/// <b>Pinned to 1 in production.</b> <see cref="Program"/> wires <see cref="MaxConcurrentMerges"/> to a
/// hardcoded 1 and does NOT bind it from <c>Merge:MaxConcurrentMerges</c>, so no appsettings/env override
/// can let two ~25 GB merges run concurrently. The setter remains so unit tests can exercise the
/// dispatcher's admission arithmetic with other caps.
/// </para>
/// </summary>
public sealed class MergeJobOptions
{
    public const string SectionName = "Merge";

    /// <summary>Max merges in flight / running at once. Always 1 in production (serialized).</summary>
    public int MaxConcurrentMerges { get; set; } = 1;
}
