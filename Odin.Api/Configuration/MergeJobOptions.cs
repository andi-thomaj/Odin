namespace Odin.Api.Configuration;

/// <summary>
/// Tuning for the AADR merge job. The merge is memory-heavy (<c>mergeit</c> on the 2M panel needs
/// ~25 GB RAM), so merges are run <b>strictly one at a time</b> by default: this caps both the
/// dispatcher's in-flight admissions and the Hangfire "merge" queue worker count. Raise only if the
/// host has the RAM headroom for concurrent merges.
/// </summary>
public sealed class MergeJobOptions
{
    public const string SectionName = "Merge";

    /// <summary>Max merges allowed in flight / running at once. Default 1 (serialized).</summary>
    public int MaxConcurrentMerges { get; set; } = 1;
}
