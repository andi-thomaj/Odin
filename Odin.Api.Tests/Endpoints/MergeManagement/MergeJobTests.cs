using System.Net;
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

    // ── helpers ───────────────────────────────────────────────────────────
    private static MergeJob CreateJob(
        ApplicationDbContext db, IMergePipelineService proxy, IBackgroundJobClient? jobClient = null,
        int maxInFlight = 2)
        => new(db, proxy, jobClient ?? new FakeJobClient(), new NoopRealtimeNotifier(),
            TimeProvider.System,
            Microsoft.Extensions.Options.Options.Create(
                new Odin.Api.Configuration.MergeJobOptions { MaxConcurrentMerges = maxInFlight }),
            NullLogger<MergeJob>.Instance);

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
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"merge-tests-{Guid.NewGuid():N}")
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

        public Task DeleteAsync(string mergeId, CancellationToken cancellationToken = default)
            => OnDelete!(mergeId);

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
}
