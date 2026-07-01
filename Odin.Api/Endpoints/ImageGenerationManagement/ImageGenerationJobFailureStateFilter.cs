using Hangfire.States;
using Hangfire.Storage;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Enums;

namespace Odin.Api.Endpoints.ImageGenerationManagement;

/// <summary>
/// Reconciles an image-generation job to <see cref="ImageGenerationStatus.Failed"/> when its Hangfire job
/// finally fails (retries exhausted) or its worker died mid-run before <c>ProcessJobAsync</c> could record
/// the failure. Without this, a worker-crash (e.g. a redeploy) would leave the row stuck Pending/Running
/// forever. Mirrors <c>MergeJobFailureStateFilter</c>.
/// </summary>
public sealed class ImageGenerationJobFailureStateFilter(IServiceScopeFactory scopeFactory) : IApplyStateFilter
{
    public void OnStateApplied(ApplyStateContext context, IWriteOnlyTransaction transaction)
    {
        if (context.NewState is not FailedState)
            return; // only once Hangfire has exhausted retries

        var job = context.BackgroundJob.Job;
        if (job is null
            || job.Type != typeof(IImageGenerationWorker)
            || job.Method.Name != nameof(IImageGenerationWorker.RunAsync)
            || job.Args.Count == 0
            || job.Args[0] is not Guid jobId)
            return;

        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Flip a still-in-flight job → Failed; a terminal write (Succeeded/Failed) is never clobbered.
        db.ImageGenerationJobs
            .Where(j => j.Id == jobId
                && (j.Status == ImageGenerationStatus.Pending || j.Status == ImageGenerationStatus.Running))
            .ExecuteUpdate(s => s
                .SetProperty(j => j.Status, ImageGenerationStatus.Failed)
                .SetProperty(j => j.ErrorCode, "worker_failed")
                .SetProperty(j => j.ErrorMessage, "The background worker failed or was interrupted.")
                .SetProperty(j => j.CompletedAt, DateTime.UtcNow));
    }

    public void OnStateUnapplied(ApplyStateContext context, IWriteOnlyTransaction transaction) { }
}
