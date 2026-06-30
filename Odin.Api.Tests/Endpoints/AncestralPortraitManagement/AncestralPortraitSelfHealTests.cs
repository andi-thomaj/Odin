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
using Odin.Api.Endpoints.AncestralPortraitManagement;
using Xunit;

namespace Odin.Api.Tests.Endpoints.AncestralPortraitManagement;

/// <summary>
/// Regression guard for the captured "Through the Ages" bug: a backend redeploy/worker-death mid-generation left the
/// set stuck at <c>Running</c> with 0 images and nothing to re-trigger it. The fix is self-healing — the recurring
/// reconcile job re-enqueues genuinely-stale Running sets, and its Hangfire filters must live on the worker INTERFACE
/// (else they are silently ignored). These run on EF InMemory (the reconcile query needs no ExecuteUpdate); the claim's
/// stale-vs-fresh behaviour is covered by the Postgres integration test.
/// </summary>
public class AncestralPortraitSelfHealTests
{
    [Fact]
    public void HangfireFilters_LiveOnTheInterface_ForRunAndReconcile()
    {
        // Hangfire reads [Queue]/[AutomaticRetry] off the INTERFACE method handed to
        // Enqueue<IAncestralPortraitWorker> / AddOrUpdate<IAncestralPortraitWorker> — a filter on the concrete class
        // is silently ignored (same load-bearing rule as IMergeJob). Assert BOTH the generation run and the recurring
        // reconcile carry them, so the reconcile lands on the right queue and neither job gets Hangfire's default retries.
        var run = typeof(IAncestralPortraitWorker).GetMethod(nameof(IAncestralPortraitWorker.RunAsync))!;
        Assert.Equal("default", run.GetCustomAttribute<QueueAttribute>()?.Queue);
        Assert.Equal(0, run.GetCustomAttribute<AutomaticRetryAttribute>()?.Attempts);

        var reconcile = typeof(IAncestralPortraitWorker).GetMethod(nameof(IAncestralPortraitWorker.ReconcileStaleRunsAsync))!;
        Assert.Equal("default", reconcile.GetCustomAttribute<QueueAttribute>()?.Queue);
        Assert.Equal(0, reconcile.GetCustomAttribute<AutomaticRetryAttribute>()?.Attempts);
    }

    [Fact]
    public async Task ReconcileStaleRuns_ReEnqueuesOnlyStaleRunningSets()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;

        var staleRunning = AddSet(db, AncestralPortraitStatus.Running, now.AddMinutes(-20));  // dead worker → re-enqueue
        AddSet(db, AncestralPortraitStatus.Running, now.AddMinutes(-1));                       // live run (heartbeat) → leave alone
        AddSet(db, AncestralPortraitStatus.Succeeded, now.AddMinutes(-20));                    // done → never
        AddSet(db, AncestralPortraitStatus.Failed, now.AddMinutes(-20));                       // terminal → never
        AddSet(db, AncestralPortraitStatus.Pending, now.AddMinutes(-20));                      // not started → never
        await db.SaveChangesAsync();

        var jobClient = new FakeJobClient();
        var svc = CreateService(db, jobClient);

        var count = await svc.ReconcileStaleRunsAsync();

        Assert.Equal(1, count);
        Assert.Equal(1, jobClient.CountOf(nameof(IAncestralPortraitWorker.RunAsync)));
        // The single re-enqueued generation targets exactly the stale Running set — not the fresh one, nor the
        // already-finished/terminal ones (which would waste a paid gpt-image-2 run or clobber a good set).
        Assert.Contains(jobClient.Jobs, j =>
            j.Method.Name == nameof(IAncestralPortraitWorker.RunAsync) && j.Args.OfType<Guid>().Contains(staleRunning));
    }

    [Fact]
    public async Task ReconcileStaleRuns_NoStaleSets_EnqueuesNothing()
    {
        await using var db = CreateDbContext();
        var now = DateTime.UtcNow;
        AddSet(db, AncestralPortraitStatus.Running, now.AddMinutes(-1)); // fresh/live only
        AddSet(db, AncestralPortraitStatus.Succeeded, now.AddMinutes(-90));
        await db.SaveChangesAsync();

        var jobClient = new FakeJobClient();
        var count = await CreateService(db, jobClient).ReconcileStaleRunsAsync();

        Assert.Equal(0, count);
        Assert.Empty(jobClient.Jobs);
    }

    private static Guid AddSet(ApplicationDbContext db, AncestralPortraitStatus status, DateTime updatedAt)
    {
        var id = Guid.NewGuid();
        db.AncestralPortraitSets.Add(new AncestralPortraitSet
        {
            Id = id,
            OrderId = 1,
            UserId = 1,
            TransactionId = id.ToString("N"),
            Status = status,
            CreatedBy = "test",
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
        });
        return id;
    }

    private static AncestralPortraitService CreateService(ApplicationDbContext db, IBackgroundJobClient jobClient) =>
        // Only dbContext + backgroundJobs + logger are touched by ReconcileStaleRunsAsync; the rest are unused here.
        new(db, null!, null!, null!, null!, jobClient, Options.Create(new AppleIapOptions()), null!,
            NullLogger<AncestralPortraitService>.Instance);

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
