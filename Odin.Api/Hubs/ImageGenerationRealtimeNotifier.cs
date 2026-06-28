using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;

namespace Odin.Api.Hubs;

/// <summary>
/// Pushes a live "an image-generation job changed" signal to the admin who started it, so an async job's
/// completion updates their UI without polling. Targets the initiating admin via <c>Clients.User(sub)</c>
/// (the job's <c>CreatedBy</c>) rather than broadcasting — this is admin-only activity.
/// </summary>
public interface IImageGenerationRealtimeNotifier
{
    /// <summary>Targeted push to the admin who started the job (async completion).</summary>
    Task NotifyJobChangedAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Broadcast that image-generation usage changed (a job reached a terminal state), so any admin's
    /// usage page refetches its first-party totals live. Minimal payload — the FE just invalidates.
    /// </summary>
    Task NotifyUsageChangedAsync(CancellationToken cancellationToken = default);
}

public sealed class ImageGenerationRealtimeNotifier(
    IHubContext<NotificationHub> hubContext,
    ApplicationDbContext dbContext,
    ILogger<ImageGenerationRealtimeNotifier> logger) : IImageGenerationRealtimeNotifier
{
    public async Task NotifyJobChangedAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        try
        {
            var job = await dbContext.ImageGenerationJobs
                .AsNoTracking()
                .Where(j => j.Id == jobId)
                .Select(j => new { j.Id, j.Status, j.CreatedBy })
                .FirstOrDefaultAsync(cancellationToken);

            if (job is null || string.IsNullOrWhiteSpace(job.CreatedBy))
                return;

            await hubContext.Clients.User(job.CreatedBy).SendAsync(
                "ImageJobChanged",
                new { jobId = job.Id, status = job.Status.ToString() },
                cancellationToken);
        }
        catch (Exception ex)
        {
            // A failed live-refresh push must never fail the job that triggered it; the client still
            // catches up via polling. Log and move on.
            logger.LogWarning(ex, "Failed to push image-job change for {JobId}.", jobId);
        }
    }

    public async Task NotifyUsageChangedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Broadcast — the usage page is AdminOnly; the payload is empty (the FE just refetches).
            await hubContext.Clients.All.SendAsync("ImageUsageChanged", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to broadcast image-usage change.");
        }
    }
}
