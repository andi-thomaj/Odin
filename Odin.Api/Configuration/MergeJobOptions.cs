namespace Odin.Api.Configuration;

/// <summary>
/// Tuning for the AADR merge job. Merges run <b>strictly one at a time, sequentially</b> — a deliberate
/// operational choice (the tools-api merges with Poseidon <c>trident</c> at ~1.3 GB, so this is no longer
/// a RAM constraint; we keep merges serialized by policy). This caps the dispatcher's in-flight
/// admissions; the Hangfire "merge" queue is likewise pinned to a single worker.
/// <para>
/// <b>Pinned to 1 in production.</b> <see cref="Program"/> wires <see cref="MaxConcurrentMerges"/> to a
/// hardcoded 1 and does NOT bind it from <c>Merge:MaxConcurrentMerges</c>, so no appsettings/env override
/// can let two merges run concurrently. The setter remains so unit tests can exercise the dispatcher's
/// admission arithmetic with other caps.
/// </para>
/// </summary>
public sealed class MergeJobOptions
{
    public const string SectionName = "Merge";

    /// <summary>Max merges in flight / running at once. Always 1 in production (serialized by policy).</summary>
    public int MaxConcurrentMerges { get; set; } = 1;
}
