using System.Reflection;
using Hangfire;
using Odin.Api.Endpoints.ImageGenerationManagement;

namespace Odin.Api.Tests.Endpoints.ImageGenerationManagement;

public class ImageGenerationWorkerTests
{
    [Fact]
    public void HangfireFilters_LiveOnTheInterface_SoQueueAndRetryActuallyTakeEffect()
    {
        // Hangfire reads [Queue]/[AutomaticRetry] off the enqueued INTERFACE method (jobs come in via
        // Enqueue<IImageGenerationWorker>(..)). On the concrete method they'd be silently ignored — the same
        // load-bearing rule as IMergeJob. Assert they're on the interface so the regression can't return.
        var run = typeof(IImageGenerationWorker).GetMethod(nameof(IImageGenerationWorker.RunAsync))!;

        Assert.Equal("default", run.GetCustomAttribute<QueueAttribute>()?.Queue);
        Assert.Equal(2, run.GetCustomAttribute<AutomaticRetryAttribute>()?.Attempts);
    }
}
