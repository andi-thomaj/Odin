using System.Net;
using System.Reflection;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.MergeManagement;
using Odin.Api.Hubs;

namespace Odin.Api.Tests.Endpoints.MergeManagement;

public class MergeJobTests
{
    [Fact]
    public async Task RunAsync_Success_ConvertsThenMerges_PersistsReady()
    {
        await using var db = CreateDbContext();
        var (inspectionId, _) = await SeedOrderAsync(db);

        var proxy = new StubMergeService
        {
            OnConvert = (_, _) => Task.FromResult(new ConvertResult("# rsid...\nrs1\t1\t100\tAG\n", "kit_23andme.txt", "ancestry")),
            OnMerge = (mergeId, _, _, _, _) => Task.FromResult(new MergeResult(mergeId, $"{mergeId}.tar.gz", 123_456_789, "HO")),
        };
        var job = CreateJob(db, proxy);

        await job.RunAsync(inspectionId);

        var file = await db.RawGeneticFiles.AsNoTracking().SingleAsync();
        Assert.Equal(MergeStatus.Ready, file.MergeStatus);
        Assert.NotNull(file.Converted23AndMeData);
        Assert.Equal("kit_23andme.txt", file.Converted23AndMeFileName);
        Assert.False(string.IsNullOrWhiteSpace(file.MergeId));
        Assert.Equal($"{file.MergeId}.tar.gz", file.MergeFileName);
        Assert.Equal(123_456_789, file.MergeSizeBytes);
        Assert.Null(file.MergeError);
    }

    [Fact]
    public async Task RunAsync_ConvertBadRequest_IsTerminalFailure_NoThrow()
    {
        await using var db = CreateDbContext();
        var (inspectionId, _) = await SeedOrderAsync(db);

        var proxy = new StubMergeService
        {
            OnConvert = (_, _) => throw new MergePipelineException(HttpStatusCode.BadRequest, "Could not recognise the vendor/format."),
        };
        var job = CreateJob(db, proxy);

        await job.RunAsync(inspectionId); // terminal → does not rethrow

        var file = await db.RawGeneticFiles.AsNoTracking().SingleAsync();
        Assert.Equal(MergeStatus.Failed, file.MergeStatus);
        Assert.Contains("recognise", file.MergeError);
    }

    [Fact]
    public async Task RunAsync_MergeFailure_IsTerminalFailure_NoThrow()
    {
        await using var db = CreateDbContext();
        var (inspectionId, _) = await SeedOrderAsync(db);

        var proxy = new StubMergeService
        {
            OnConvert = (_, _) => Task.FromResult(new ConvertResult("x", "x_23andme.txt", "23andme")),
            OnMerge = (_, _, _, _, _) => throw new MergePipelineException(HttpStatusCode.InternalServerError, "mergeit failed (exit -9)"),
        };
        var job = CreateJob(db, proxy);

        // No automatic retries: any merge failure is terminal (recorded Failed), and RunAsync does not
        // rethrow — an admin re-runs it via RequeueAsync.
        await job.RunAsync(inspectionId);

        var file = await db.RawGeneticFiles.AsNoTracking().SingleAsync();
        Assert.Equal(MergeStatus.Failed, file.MergeStatus);
        Assert.Contains("mergeit", file.MergeError);
    }

    [Fact]
    public async Task RunAsync_MergeFailure_DropsAnyProducedBundle_SoNoTrashIsLeft()
    {
        // A merge that fails AFTER the tools-api may have produced a bundle (the timeout / lost-response
        // case) must drop it, or it orphans a .tar.gz that no sweep reclaims and the disk slowly fills.
        await using var db = CreateDbContext();
        var (inspectionId, _) = await SeedOrderAsync(db);

        string? cancelled = null;
        var proxy = new StubMergeService
        {
            OnConvert = (_, _) => Task.FromResult(new ConvertResult("x", "x_23andme.txt", "23andme")),
            OnMerge = (_, _, _, _, _) => throw new MergePipelineException(HttpStatusCode.InternalServerError, "trident failed"),
            OnCancel = id => { cancelled = id; return Task.CompletedTask; },
        };
        var job = CreateJob(db, proxy);

        await job.RunAsync(inspectionId);

        var file = await db.RawGeneticFiles.AsNoTracking().SingleAsync();
        Assert.Equal(MergeStatus.Failed, file.MergeStatus);
        // The merge reached the Merging step, so a mergeId was assigned and the bundle was dropped for it.
        Assert.False(string.IsNullOrWhiteSpace(file.MergeId));
        Assert.Equal(file.MergeId, cancelled);
    }

    [Fact]
    public async Task RunAsync_ConvertFailure_DoesNotAttemptBundleCleanup()
    {
        // A failure BEFORE the merge step never assigned a mergeId and the tools-api wrote nothing, so we
        // must not make a spurious cancel/delete call for a bundle that can't exist.
        await using var db = CreateDbContext();
        var (inspectionId, _) = await SeedOrderAsync(db);

        var cancelCalled = false;
        var proxy = new StubMergeService
        {
            OnConvert = (_, _) => throw new MergePipelineException(HttpStatusCode.BadRequest, "bad upload"),
            OnCancel = _ => { cancelCalled = true; return Task.CompletedTask; },
        };
        var job = CreateJob(db, proxy);

        await job.RunAsync(inspectionId);

        var file = await db.RawGeneticFiles.AsNoTracking().SingleAsync();
        Assert.Equal(MergeStatus.Failed, file.MergeStatus);
        Assert.Null(file.MergeId);
        Assert.False(cancelCalled);
    }

    [Fact]
    public async Task RunAsync_AlreadyReady_IsNoOp()
    {
        await using var db = CreateDbContext();
        var (inspectionId, fileId) = await SeedOrderAsync(db);
        var existing = await db.RawGeneticFiles.SingleAsync(f => f.Id == fileId);
        existing.MergeStatus = MergeStatus.Ready;
        existing.MergeId = "insp-1-abc";
        await db.SaveChangesAsync();

        var proxy = new StubMergeService
        {
            OnConvert = (_, _) => throw new InvalidOperationException("must not re-run a ready merge"),
        };
        var job = CreateJob(db, proxy);

        await job.RunAsync(inspectionId);

        var file = await db.RawGeneticFiles.AsNoTracking().SingleAsync();
        Assert.Equal(MergeStatus.Ready, file.MergeStatus);
        Assert.Equal("insp-1-abc", file.MergeId);
    }

    [Fact]
    public async Task RunAsync_EnqueuesDispatchOnCompletion_ToRefillCapacity()
    {
        await using var db = CreateDbContext();
        var (inspectionId, _) = await SeedOrderAsync(db);
        var proxy = new StubMergeService
        {
            OnConvert = (_, _) => Task.FromResult(new ConvertResult("x", "x_23andme.txt", "23andme")),
            OnMerge = (mergeId, _, _, _, _) => Task.FromResult(new MergeResult(mergeId, $"{mergeId}.tar.gz", 1, "HO")),
        };
        var jobClient = new FakeJobClient();
        var job = CreateJob(db, proxy, jobClient);

        await job.RunAsync(inspectionId);

        Assert.Equal(1, jobClient.CountOf(nameof(IMergeJob.DispatchPendingMergesAsync)));
    }

    [Fact]
    public async Task Dispatch_AdmitsUpToCap_LeavesRestQueued()
    {
        await using var db = CreateDbContext();
        await SeedOrderAsync(db);
        await SeedOrderAsync(db);
        await SeedOrderAsync(db); // 3 NotStarted; cap is 2

        var jobClient = new FakeJobClient();
        var job = CreateJob(db, new StubMergeService(), jobClient);

        await job.DispatchPendingMergesAsync();

        var statuses = await db.RawGeneticFiles.AsNoTracking().Select(f => f.MergeStatus).ToListAsync();
        Assert.Equal(2, statuses.Count(s => s == MergeStatus.Queued));
        Assert.Equal(1, statuses.Count(s => s == MergeStatus.NotStarted));
        Assert.Equal(2, jobClient.CountOf(nameof(IMergeJob.RunAsync)));
    }

    [Fact]
    public async Task Dispatch_NoCapacity_WhenCapInFlight_AdmitsNone()
    {
        await using var db = CreateDbContext();
        var (_, f1) = await SeedOrderAsync(db);
        var (_, f2) = await SeedOrderAsync(db);
        await SeedOrderAsync(db); // 1 NotStarted

        // Saturate the in-flight set (Converting + Merging == cap of 2).
        (await db.RawGeneticFiles.SingleAsync(f => f.Id == f1)).MergeStatus = MergeStatus.Converting;
        (await db.RawGeneticFiles.SingleAsync(f => f.Id == f2)).MergeStatus = MergeStatus.Merging;
        await db.SaveChangesAsync();

        var jobClient = new FakeJobClient();
        var job = CreateJob(db, new StubMergeService(), jobClient);

        await job.DispatchPendingMergesAsync();

        Assert.Equal(0, jobClient.CountOf(nameof(IMergeJob.RunAsync)));
        Assert.Equal(1, await db.RawGeneticFiles.CountAsync(f => f.MergeStatus == MergeStatus.NotStarted));
    }

    [Fact]
    public void HangfireFilters_LiveOnTheInterface_SoSerializationActuallyTakesEffect()
    {
        // The merges-running-concurrently regression guard. Hangfire reads job filter attributes off the
        // ENQUEUED method, and every merge job is enqueued via the INTERFACE
        // (Enqueue<IMergeJob>(..) / AddOrUpdate<IMergeJob>(..) in Program.cs / MergeJob). If a filter drifts
        // onto the concrete MergeJob it is silently ignored:
        //   • [DisableConcurrentExecution] off the dispatcher → concurrent dispatches race the count→admit
        //     step and over-admit past the cap → several merges show "Merging" at once (the reported bug);
        //   • [Queue("merge")] off RunAsync → merges land on the multi-worker "default" queue and run in
        //     parallel; [AutomaticRetry(0)] off RunAsync → a failed merge takes Hangfire's default 10 retries.
        // Assert each lives on the interface method so none of that can return unnoticed.
        var dispatch = typeof(IMergeJob).GetMethod(nameof(IMergeJob.DispatchPendingMergesAsync))!;
        Assert.NotNull(dispatch.GetCustomAttribute<DisableConcurrentExecutionAttribute>());

        var run = typeof(IMergeJob).GetMethod(nameof(IMergeJob.RunAsync))!;
        Assert.Equal("merge", run.GetCustomAttribute<QueueAttribute>()?.Queue);
        Assert.Equal(0, run.GetCustomAttribute<AutomaticRetryAttribute>()?.Attempts);

        var cleanup = typeof(IMergeJob).GetMethod(nameof(IMergeJob.CleanupOrphansAsync))!;
        Assert.Equal("merge", cleanup.GetCustomAttribute<QueueAttribute>()?.Queue);
    }

    [Fact]
    public async Task Dispatch_CapOfOne_AdmitsExactlyOne_LeavesRestQueued()
    {
        // Production pins the in-flight cap to 1 (Program.cs hardwires MaxConcurrentMerges = 1). With three
        // waiting (NotStarted) merges and nothing running, a single dispatch must admit EXACTLY one and
        // leave the other two waiting — never more than one merge in flight.
        await using var db = CreateDbContext();
        await SeedOrderAsync(db);
        await SeedOrderAsync(db);
        await SeedOrderAsync(db);

        var jobClient = new FakeJobClient();
        var job = CreateJob(db, new StubMergeService(), jobClient, maxInFlight: 1);

        await job.DispatchPendingMergesAsync();

        var statuses = await db.RawGeneticFiles.AsNoTracking().Select(f => f.MergeStatus).ToListAsync();
        Assert.Equal(1, statuses.Count(s => s == MergeStatus.Queued));
        Assert.Equal(2, statuses.Count(s => s == MergeStatus.NotStarted));
        Assert.Equal(1, jobClient.CountOf(nameof(IMergeJob.RunAsync)));
    }

    [Fact]
    public async Task Dispatch_CapOfOne_WithOneAlreadyMerging_AdmitsNone()
    {
        // The exact reported failure mode: one merge already "Merging" must block every other waiting merge,
        // so a concurrently-fired dispatch admits nothing (the count→admit arithmetic the
        // [DisableConcurrentExecution] lock protects from racing in production).
        await using var db = CreateDbContext();
        var (_, f1) = await SeedOrderAsync(db);
        await SeedOrderAsync(db);
        await SeedOrderAsync(db);
        (await db.RawGeneticFiles.SingleAsync(f => f.Id == f1)).MergeStatus = MergeStatus.Merging;
        await db.SaveChangesAsync();

        var jobClient = new FakeJobClient();
        var job = CreateJob(db, new StubMergeService(), jobClient, maxInFlight: 1);

        await job.DispatchPendingMergesAsync();

        Assert.Equal(0, jobClient.CountOf(nameof(IMergeJob.RunAsync)));
        Assert.Equal(1, await db.RawGeneticFiles.CountAsync(f => f.MergeStatus == MergeStatus.Merging));
        Assert.Equal(2, await db.RawGeneticFiles.CountAsync(f => f.MergeStatus == MergeStatus.NotStarted));
    }

    [Fact]
    public async Task DeleteAsync_CallsProxy_MarksDeleted()
    {
        await using var db = CreateDbContext();
        var (_, fileId) = await SeedOrderAsync(db);
        var file = await db.RawGeneticFiles.SingleAsync(f => f.Id == fileId);
        file.MergeStatus = MergeStatus.Ready;
        file.MergeId = "insp-1-xyz";
        file.MergeFileName = "insp-1-xyz.tar.gz";
        file.MergeSizeBytes = 999;
        await db.SaveChangesAsync();

        string? deleted = null;
        var proxy = new StubMergeService { OnDelete = id => { deleted = id; return Task.CompletedTask; } };
        var job = CreateJob(db, proxy);

        await job.DeleteAsync(fileId);

        Assert.Equal("insp-1-xyz", deleted);
        var after = await db.RawGeneticFiles.AsNoTracking().SingleAsync();
        Assert.Equal(MergeStatus.Deleted, after.MergeStatus);
        Assert.Null(after.MergeFileName);
        Assert.Null(after.MergeSizeBytes);
    }

    [Fact]
    public async Task DeleteAsync_NoMergeId_IsNoOp()
    {
        await using var db = CreateDbContext();
        var (_, fileId) = await SeedOrderAsync(db);

        var called = false;
        var proxy = new StubMergeService { OnDelete = _ => { called = true; return Task.CompletedTask; } };
        var job = CreateJob(db, proxy);

        await job.DeleteAsync(fileId);

        Assert.False(called);
    }

    [Fact]
    public async Task CleanupOrphans_DeletesReadyBundleOlderThanRetentionHours()
    {
        await using var db = CreateDbContext();
        var (_, fileId) = await SeedOrderAsync(db); // order stays Pending → only retention applies
        var file = await db.RawGeneticFiles.SingleAsync(f => f.Id == fileId);
        file.MergeStatus = MergeStatus.Ready;
        file.MergeId = "insp-1-stale";
        file.MergeFileName = "insp-1-stale.tar.gz";
        file.MergeSizeBytes = 999;
        file.UpdatedAt = DateTime.UtcNow.AddHours(-25); // just past the 24h window
        await db.SaveChangesAsync();

        string? deleted = null;
        var proxy = new StubMergeService { OnDelete = id => { deleted = id; return Task.CompletedTask; } };
        var job = CreateJob(db, proxy);

        await job.CleanupOrphansAsync();

        Assert.Equal("insp-1-stale", deleted);
        var after = await db.RawGeneticFiles.AsNoTracking().SingleAsync();
        Assert.Equal(MergeStatus.Deleted, after.MergeStatus);
    }

    [Fact]
    public async Task CleanupOrphans_KeepsRecentUnconsumedBundle()
    {
        await using var db = CreateDbContext();
        var (_, fileId) = await SeedOrderAsync(db); // order stays Pending → only retention applies
        var file = await db.RawGeneticFiles.SingleAsync(f => f.Id == fileId);
        file.MergeStatus = MergeStatus.Ready;
        file.MergeId = "insp-1-fresh";
        file.UpdatedAt = DateTime.UtcNow.AddHours(-1); // well within the 24h window
        await db.SaveChangesAsync();

        var called = false;
        var proxy = new StubMergeService { OnDelete = _ => { called = true; return Task.CompletedTask; } };
        var job = CreateJob(db, proxy);

        await job.CleanupOrphansAsync();

        Assert.False(called);
        var after = await db.RawGeneticFiles.AsNoTracking().SingleAsync();
        Assert.Equal(MergeStatus.Ready, after.MergeStatus);
    }

    [Fact]
    public async Task CleanupOrphans_ReclaimsOrphanedFailedBundle_KeepsFailedStatus_ClearsMergeId()
    {
        // Backstop for the worker-died case: a row reconciled to Failed but with a bundle still on the
        // volume. The sweep must drop that bundle (so it can't fill the disk) while preserving the Failed
        // status + error, and detach the merge id so it isn't revisited.
        await using var db = CreateDbContext();
        var (_, fileId) = await SeedOrderAsync(db);
        var file = await db.RawGeneticFiles.SingleAsync(f => f.Id == fileId);
        file.MergeStatus = MergeStatus.Failed;
        file.MergeError = "trident failed (exit -9)";
        file.MergeId = "insp-1-orphan";
        file.MergeFileName = "insp-1-orphan.tar.gz";
        file.MergeSizeBytes = 999;
        await db.SaveChangesAsync();

        string? deleted = null;
        var proxy = new StubMergeService { OnDelete = id => { deleted = id; return Task.CompletedTask; } };
        var job = CreateJob(db, proxy);

        await job.CleanupOrphansAsync();

        Assert.Equal("insp-1-orphan", deleted);
        var after = await db.RawGeneticFiles.AsNoTracking().SingleAsync();
        Assert.Equal(MergeStatus.Failed, after.MergeStatus); // status + error preserved
        Assert.Equal("trident failed (exit -9)", after.MergeError);
        Assert.Null(after.MergeId); // detached so the next sweep won't revisit it
        Assert.Null(after.MergeFileName);
        Assert.Null(after.MergeSizeBytes);
    }

    [Fact]
    public async Task CleanupOrphans_FailedWithoutMergeId_IsLeftAlone()
    {
        // A merge that failed before producing anything has no merge id → no bundle to reclaim.
        await using var db = CreateDbContext();
        var (_, fileId) = await SeedOrderAsync(db);
        var file = await db.RawGeneticFiles.SingleAsync(f => f.Id == fileId);
        file.MergeStatus = MergeStatus.Failed;
        file.MergeError = "bad upload";
        await db.SaveChangesAsync();

        var called = false;
        var proxy = new StubMergeService { OnDelete = _ => { called = true; return Task.CompletedTask; } };
        var job = CreateJob(db, proxy);

        await job.CleanupOrphansAsync();

        Assert.False(called);
        var after = await db.RawGeneticFiles.AsNoTracking().SingleAsync();
        Assert.Equal(MergeStatus.Failed, after.MergeStatus);
    }

    [Fact]
    public async Task DeleteAllReadyMergedData_DeletesEveryReadyBundle_AndReturnsCount()
    {
        await using var db = CreateDbContext();
        await SeedReadyMergeAsync(db, "insp-a");
        await SeedReadyMergeAsync(db, "insp-b");
        await SeedOrderAsync(db); // NotStarted — must be left untouched

        var deletedIds = new List<string>();
        var proxy = new StubMergeService { OnDelete = id => { deletedIds.Add(id); return Task.CompletedTask; } };
        var job = CreateJob(db, proxy);

        var count = await job.DeleteAllReadyMergedDataAsync();

        Assert.Equal(2, count);
        Assert.Equal(new[] { "insp-a", "insp-b" }, deletedIds.OrderBy(x => x).ToArray());
        Assert.Equal(2, await db.RawGeneticFiles.CountAsync(f => f.MergeStatus == MergeStatus.Deleted));
        Assert.Equal(1, await db.RawGeneticFiles.CountAsync(f => f.MergeStatus == MergeStatus.NotStarted));
    }

    [Fact]
    public async Task DeleteAllReadyMergedData_CountsOnlyBundlesItActuallyFreed()
    {
        await using var db = CreateDbContext();
        await SeedReadyMergeAsync(db, "insp-a");
        await SeedReadyMergeAsync(db, "insp-b");

        // Simulate a concurrent delete: while the sweep deletes its first bundle, another path marks
        // every OTHER Ready bundle Deleted. Those later iterations become no-ops and must NOT be counted.
        var firstCall = true;
        var proxy = new StubMergeService
        {
            OnDelete = async mergeId =>
            {
                if (!firstCall) return;
                firstCall = false;
                var others = await db.RawGeneticFiles
                    .Where(f => f.MergeStatus == MergeStatus.Ready && f.MergeId != mergeId)
                    .ToListAsync();
                foreach (var o in others)
                    o.MergeStatus = MergeStatus.Deleted;
                await db.SaveChangesAsync();
            },
        };
        var job = CreateJob(db, proxy);

        var count = await job.DeleteAllReadyMergedDataAsync();

        Assert.Equal(1, count); // only the bundle this call actually freed is counted
        Assert.Equal(2, await db.RawGeneticFiles.CountAsync(f => f.MergeStatus == MergeStatus.Deleted));
    }

    [Fact]
    public async Task DeleteAllReadyMergedData_WhenNoneReady_ReturnsZero_AndCallsNothing()
    {
        await using var db = CreateDbContext();
        await SeedOrderAsync(db); // NotStarted only — nothing to delete

        var called = false;
        var proxy = new StubMergeService { OnDelete = _ => { called = true; return Task.CompletedTask; } };
        var job = CreateJob(db, proxy);

        var count = await job.DeleteAllReadyMergedDataAsync();

        Assert.Equal(0, count);
        Assert.False(called);
    }

    [Fact]
    public async Task RequeueAsync_FailedMerge_ResetsToNotStarted_AndDispatches()
    {
        await using var db = CreateDbContext();
        var (_, fileId) = await SeedOrderAsync(db);
        var f = await db.RawGeneticFiles.SingleAsync(x => x.Id == fileId);
        f.MergeStatus = MergeStatus.Failed;
        f.MergeError = "mergeit failed (exit -9)";
        await db.SaveChangesAsync();

        var jobClient = new FakeJobClient();
        var job = CreateJob(db, new StubMergeService(), jobClient);

        await job.RequeueAsync(fileId);

        var after = await db.RawGeneticFiles.AsNoTracking().SingleAsync();
        Assert.Equal(MergeStatus.NotStarted, after.MergeStatus);
        Assert.Null(after.MergeError);
        Assert.Equal(1, jobClient.CountOf(nameof(IMergeJob.DispatchPendingMergesAsync)));
    }

    [Fact]
    public async Task RequeueAsync_InProgress_Throws()
    {
        await using var db = CreateDbContext();
        var (_, fileId) = await SeedOrderAsync(db);
        var f = await db.RawGeneticFiles.SingleAsync(x => x.Id == fileId);
        f.MergeStatus = MergeStatus.Merging;
        await db.SaveChangesAsync();

        var job = CreateJob(db, new StubMergeService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.RequeueAsync(fileId));
    }

    [Fact]
    public async Task StopAsync_InProgress_KillsToolsApi_MarksFailed_AndDispatches()
    {
        await using var db = CreateDbContext();
        var (_, fileId) = await SeedOrderAsync(db);
        var f = await db.RawGeneticFiles.SingleAsync(x => x.Id == fileId);
        f.MergeStatus = MergeStatus.Merging;
        f.MergeId = "insp-1-abc";
        await db.SaveChangesAsync();

        string? cancelled = null;
        var proxy = new StubMergeService { OnCancel = id => { cancelled = id; return Task.CompletedTask; } };
        var jobClient = new FakeJobClient();
        var job = CreateJob(db, proxy, jobClient);

        await job.StopAsync(fileId);

        var after = await db.RawGeneticFiles.AsNoTracking().SingleAsync();
        Assert.Equal(MergeStatus.Failed, after.MergeStatus);
        Assert.Equal("insp-1-abc", cancelled); // tools-api forge was told to stop
        Assert.Contains("stopped", after.MergeError, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, jobClient.CountOf(nameof(IMergeJob.DispatchPendingMergesAsync)));
    }

    [Fact]
    public async Task StopAsync_NotInProgress_Throws()
    {
        await using var db = CreateDbContext();
        var (_, fileId) = await SeedOrderAsync(db);
        var f = await db.RawGeneticFiles.SingleAsync(x => x.Id == fileId);
        f.MergeStatus = MergeStatus.Failed; // not Queued/Converting/Merging → nothing to stop
        await db.SaveChangesAsync();

        var job = CreateJob(db, new StubMergeService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => job.StopAsync(fileId));
    }

    [Fact]
    public async Task StopAsync_MissingFile_ThrowsKeyNotFound()
    {
        await using var db = CreateDbContext();
        var job = CreateJob(db, new StubMergeService());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => job.StopAsync(999_999));
    }

    // ── helpers ───────────────────────────────────────────────────────────
    private static MergeJob CreateJob(
        ApplicationDbContext db, IMergePipelineService proxy, IBackgroundJobClient? jobClient = null,
        int maxInFlight = 2, JobStorage? jobStorage = null)
        => new(db, proxy, jobClient ?? new FakeJobClient(), jobStorage ?? new FakeJobStorage(),
            new NoopRealtimeNotifier(),
            TimeProvider.System,
            Microsoft.Extensions.Options.Options.Create(
                new Odin.Api.Configuration.MergeJobOptions { MaxConcurrentMerges = maxInFlight }),
            NullLogger<MergeJob>.Instance);

    /// <summary>Seed an order whose merge is already <c>Ready</c> with a bundle (a deletable bundle).</summary>
    private static async Task<int> SeedReadyMergeAsync(ApplicationDbContext db, string mergeId)
    {
        var (_, fileId) = await SeedOrderAsync(db);
        var file = await db.RawGeneticFiles.SingleAsync(f => f.Id == fileId);
        file.MergeStatus = MergeStatus.Ready;
        file.MergeId = mergeId;
        file.MergeFileName = $"{mergeId}.tar.gz";
        file.MergeSizeBytes = 123;
        await db.SaveChangesAsync();
        return fileId;
    }

    private static async Task<(int inspectionId, int fileId)> SeedOrderAsync(ApplicationDbContext db)
    {
        var now = DateTime.UtcNow;
        var rawFile = new RawGeneticFile
        {
            RawDataFileName = "sample.txt",
            RawData = "rs1\t1\t100\tAG\n"u8.ToArray(),
            CreatedBy = "t",
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.RawGeneticFiles.Add(rawFile);

        var order = new QpadmOrder { Status = OrderStatus.Pending, CreatedBy = "t", CreatedAt = now, UpdatedAt = now };
        db.QpadmOrders.Add(order);
        await db.SaveChangesAsync();

        var inspection = new QpadmGeneticInspection
        {
            FirstName = "Test",
            MiddleName = "",
            LastName = "User",
            Gender = Gender.Male,
            RawGeneticFileId = rawFile.Id,
            UserId = 1,
            OrderId = order.Id,
            CreatedBy = "t",
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.QpadmGeneticInspections.Add(inspection);
        await db.SaveChangesAsync();
        return (inspection.Id, rawFile.Id);
    }

    private static ApplicationDbContext CreateDbContext()
        => CreateDbContext($"merge-tests-{Guid.NewGuid():N}");

    private static ApplicationDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class NoopRealtimeNotifier : IGeneticInspectionRealtimeNotifier
    {
        public Task NotifyChangedAsync(string reason, int? inspectionId = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class StubMergeService : IMergePipelineService
    {
        public Func<byte[], string, Task<ConvertResult>>? OnConvert { get; set; }
        public Func<string, string, string?, string?, string, Task<MergeResult>>? OnMerge { get; set; }
        public Func<string, Task>? OnDelete { get; set; }

        public Task<ConvertResult> ConvertAsync(byte[] raw, string fileName, CancellationToken cancellationToken = default)
            => OnConvert!(raw, fileName);

        public Task<MergeResult> RunMergeAsync(
            string mergeId, string converted23Andme, string? panel, string? sampleId, string sex,
            CancellationToken cancellationToken = default)
            => OnMerge!(mergeId, converted23Andme, panel, sampleId, sex);

        public Task<HttpResponseMessage> OpenDownloadAsync(string mergeId, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Func<string, Task>? OnCancel { get; set; }

        public Task DeleteAsync(string mergeId, CancellationToken cancellationToken = default)
            => OnDelete!(mergeId);

        public Task CancelMergeAsync(string mergeId, CancellationToken cancellationToken = default)
            => (OnCancel ?? (_ => Task.CompletedTask))(mergeId);

        // The merge job never touches panel restore; these are admin-endpoint-only.
        public Task<PanelStatusResult> GetPanelStatusAsync(string? panel, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PanelUploadResult> UploadPanelFileAsync(
            string ext, string? panel, string? sha256, Stream body, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PanelActivateResult> ActivatePanelAsync(
            string? panel, bool force, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PanelIndRowsResult> GetPanelIndRowsAsync(
            string? panel, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PanelIndRowResult> SetPanelIndRowLabelAsync(
            string? panel, int index, string label, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();

        public Task<PanelRenameLabelResult> RenamePanelLabelAsync(
            string? panel, string fromLabel, string toLabel, CancellationToken cancellationToken = default)
            => throw new NotImplementedException();
    }

    private sealed class FakeJobClient : IBackgroundJobClient
    {
        public List<Job> Jobs { get; } = [];
        public string Create(Job job, IState state) { Jobs.Add(job); return Guid.NewGuid().ToString("N"); }
        public bool ChangeState(string jobId, IState state, string expectedState) => true;
        public int CountOf(string methodName) => Jobs.Count(j => j.Method.Name == methodName);
    }

    // StopAsync needs a JobStorage for its monitoring-API scan, but the guard paths (missing file /
    // not-in-progress) return before touching it, so a throw-only stub is enough for these tests.
    // The monitoring scan itself is Hangfire glue, verified end-to-end rather than here.
    private sealed class FakeJobStorage : JobStorage
    {
        public override global::Hangfire.Storage.IStorageConnection GetConnection() => throw new NotImplementedException();
        public override global::Hangfire.Storage.IMonitoringApi GetMonitoringApi() => throw new NotImplementedException();
    }
}
