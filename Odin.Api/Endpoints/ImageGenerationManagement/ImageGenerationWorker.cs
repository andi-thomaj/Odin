using Odin.Api.Hubs;

namespace Odin.Api.Endpoints.ImageGenerationManagement;

public sealed class ImageGenerationWorker(
    IImageGenerationService service,
    IImageGenerationRealtimeNotifier notifier,
    ILogger<ImageGenerationWorker> logger) : IImageGenerationWorker
{
    // [Queue]/[AutomaticRetry] live on IImageGenerationWorker (the interface) — NOT here — because Hangfire
    // reads filter attributes off the enqueued interface method; on this concrete method they'd be ignored.
    public async Task RunAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            await service.ProcessJobAsync(jobId, cancellationToken);
        }
        catch (OpenAIImageException ex)
        {
            // ProcessJobAsync already recorded the job Failed. Push the update, then decide on retry:
            // a transient (429/5xx) rethrows so Hangfire retries; a terminal error (moderation, bad
            // request) is swallowed so the same prompt isn't re-attempted.
            await notifier.NotifyJobChangedAsync(jobId, CancellationToken.None);
            if (ex.IsTransient)
            {
                logger.LogWarning(ex, "Image job {JobId} hit a transient OpenAI error; will retry.", jobId);
                throw;
            }

            logger.LogInformation("Image job {JobId} failed terminally ({Code}); not retrying.", jobId, ex.Code);
            return;
        }

        await notifier.NotifyJobChangedAsync(jobId, CancellationToken.None);
    }
}
