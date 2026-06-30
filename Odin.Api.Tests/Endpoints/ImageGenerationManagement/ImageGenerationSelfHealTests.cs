using System.Reflection;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.ImageGenerationManagement;
using Xunit;

namespace Odin.Api.Tests.Endpoints.ImageGenerationManagement;

/// <summary>
/// Regression guard for the deploy-interruption bug class applied to admin image generation: a redeploy/worker-death
/// mid-run must not strand a job at <c>Running</c> forever. The recurring reconcile re-enqueues genuinely-stale Running
/// jobs, and its Hangfire filters must live on the worker INTERFACE. (The claim's stale-vs-fresh behaviour — which uses
/// ExecuteUpdate, unsupported by EF InMemory — is covered by the Postgres integration test.)
/// </summary>
public class ImageGenerationSelfHealTests
{
    [Fact]
    public void HangfireFilters_LiveOnTheInterface_ForRunAndReconcile()
    {
        var run = typeof(IImageGenerationWorker).GetMethod(nameof(IImageGenerationWorker.RunAsync))!;
        Assert.Equal("default", run.GetCustomAttribute<QueueAttribute>()?.Queue);
        Assert.Equal(2, run.GetCustomAttribute<AutomaticRetryAttribute>()?.Attempts); // transient 429/5xx retries

        var reconcile = typeof(IImageGenerationWorker).GetMethod(nameof(IImageGenerationWorker.ReconcileStaleJobsAsync))!;
        Assert.Equal("default", reconcile.GetCustomAttribute<QueueAttribute>()?.Queue);
        Assert.Equal(0, reconcile.GetCustomAttribute<AutomaticRetryAttribute>()?.Attempts);
    }

    [Fact]
    public async Task ReconcileStaleJobs_ReEnqueuesOnlyStaleRunningJobs()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        var staleRunning = AddJob(db, ImageGenerationStatus.Running, now.AddMinutes(-30)); // dead worker → re-enqueue
        AddJob(db, ImageGenerationStatus.Running, now.AddMinutes(-2));                      // live run → leave alone
        AddJob(db, ImageGenerationStatus.Succeeded, now.AddMinutes(-30));                   // done → never
        AddJob(db, ImageGenerationStatus.Failed, now.AddMinutes(-30));                      // terminal → never
        AddJob(db, ImageGenerationStatus.Pending, now.AddMinutes(-30));                     // not started → never
        await db.SaveChangesAsync();

        var jobClient = new FakeJobClient();
        var count = await CreateService(db, jobClient).ReconcileStaleJobsAsync();

        Assert.Equal(1, count);
        Assert.Equal(1, jobClient.CountOf(nameof(IImageGenerationWorker.RunAsync)));
        Assert.Contains(jobClient.Jobs, j =>
            j.Method.Name == nameof(IImageGenerationWorker.RunAsync) && j.Args.OfType<Guid>().Contains(staleRunning));
    }

    private static Guid AddJob(ApplicationDbContext db, ImageGenerationStatus status, DateTime updatedAt)
    {
        var id = Guid.NewGuid();
        db.ImageGenerationJobs.Add(new ImageGenerationJob
        {
            Id = id,
            Mode = ImageGenerationMode.Generation,
            Status = status,
            Prompt = "p",
            Model = "gpt-image-2",
            Size = "1024x1024",
            Quality = "low",
            N = 1,
            CreatedBy = "test",
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
        });
        return id;
    }

    private static ImageGenerationService CreateService(ApplicationDbContext db, IBackgroundJobClient jobClient) =>
        // Only dbContext + backgroundJobs + logger are touched by ReconcileStaleJobsAsync; the rest are unused here.
        new(db, null!, null!, null!, null!, jobClient, Options.Create(new ImageGenerationLimitsOptions()),
            NullLogger<ImageGenerationService>.Instance);

    private static ApplicationDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private sealed class FakeJobClient : IBackgroundJobClient
    {
        public List<Job> Jobs { get; } = [];
        public string Create(Job job, IState state)
        {
            Jobs.Add(job);
            return Guid.NewGuid().ToString("N");
        }
        public bool ChangeState(string jobId, IState state, string expectedState) => true;
        public int CountOf(string methodName) => Jobs.Count(j => j.Method.Name == methodName);
    }
}
