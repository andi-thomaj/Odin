using Hangfire;

namespace Odin.Api.Endpoints.AncestralPortraitManagement;

/// <summary>
/// Background worker that runs an ancestral-portrait generation (multiple gpt-image-2 edit calls). Enqueued by the
/// purchase/generate endpoints; the iOS client polls the set for status.
/// </summary>
/// <remarks>
/// The <c>[Queue]</c>/<c>[AutomaticRetry]</c> attributes MUST live on this <b>interface</b> method (Hangfire reads
/// filter attributes off the interface handed to <c>Enqueue&lt;IAncestralPortraitWorker&gt;</c>; a concrete-method
/// attribute is silently ignored — same load-bearing rule as <c>IMergeJob</c>/<c>IImageGenerationWorker</c>).
/// <c>Attempts = 0</c>: generation is expensive + cost-bounded, and <c>RunGenerationAsync</c> already handles per-era
/// upstream failures gracefully (records <c>Failed</c>, never rethrows), so a blanket retry would only burn cost.
/// </remarks>
public interface IAncestralPortraitWorker
{
    [Queue("default")]
    [AutomaticRetry(Attempts = 0)]
    Task RunAsync(Guid setId, CancellationToken cancellationToken = default);

    /// <summary>Recurring self-heal: re-enqueue sets stuck <c>Running</c> past the stale window (worker death/redeploy).
    /// <c>[Queue]</c>/<c>[AutomaticRetry]</c> live on the interface (same load-bearing rule as <see cref="RunAsync"/>).</summary>
    [Queue("default")]
    [AutomaticRetry(Attempts = 0)]
    Task ReconcileStaleRunsAsync(CancellationToken cancellationToken = default);
}

public sealed class AncestralPortraitWorker(IAncestralPortraitService service) : IAncestralPortraitWorker
{
    public Task RunAsync(Guid setId, CancellationToken cancellationToken = default) =>
        service.RunGenerationAsync(setId, cancellationToken);

    public Task ReconcileStaleRunsAsync(CancellationToken cancellationToken = default) =>
        service.ReconcileStaleRunsAsync(cancellationToken);
}
