using Hangfire;

namespace Odin.Api.Endpoints.ImageGenerationManagement;

/// <summary>
/// Background worker for async image-generation jobs, run via Hangfire and enqueued by the generate
/// endpoints when a request opts into <c>async</c>. Named "Worker" to avoid colliding with the
/// <c>ImageGenerationJob</c> entity.
/// </summary>
/// <remarks>
/// The <c>[Queue]</c>/<c>[AutomaticRetry]</c> attributes MUST live on this <b>interface</b> method, not the
/// concrete class. Jobs are enqueued via <c>Enqueue&lt;IImageGenerationWorker&gt;(w =&gt; w.RunAsync(..))</c>,
/// so Hangfire reads filter attributes off the interface method it was handed; an attribute on the concrete
/// method is silently ignored. Same load-bearing rule as <c>IMergeJob</c> — asserted by a reflection test.
/// Runs on the standard <c>default</c> queue (not the serialized <c>merge</c> queue); a small retry count
/// covers transient 429/5xx, while terminal failures (moderation, bad request) are recorded Failed and not
/// retried.
/// </remarks>
public interface IImageGenerationWorker
{
    [Queue("default")]
    [AutomaticRetry(Attempts = 2)]
    Task RunAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>Recurring self-heal: re-enqueue jobs stuck <c>Running</c> past the stale window (worker death/redeploy).
    /// <c>[Queue]</c>/<c>[AutomaticRetry]</c> live on the interface (same load-bearing rule as <see cref="RunAsync"/>).</summary>
    [Queue("default")]
    [AutomaticRetry(Attempts = 0)]
    Task ReconcileStaleJobsAsync(CancellationToken cancellationToken = default);
}
