using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.GeneticInspectionManagement;
using Odin.Api.Endpoints.GeneticInspectionManagement.Models;
using Odin.Api.Endpoints.NotificationManagement;
using Odin.Api.Endpoints.NotificationManagement.Models;
using Odin.Api.Hubs;

namespace Odin.Api.Tests.Endpoints.GeneticInspectionManagement;

/// <summary>
/// A Scientist/Admin must not be able to submit qpAdm results until the AADR merge has finished.
/// Enforced in the service so it holds regardless of caller; these cover the guard's decision per
/// merge state. (Authorization to the ScientistOrAdmin role is enforced on the endpoint.)
/// </summary>
public class SubmitQpadmResultMergeGuardTests
{
    [Theory]
    [InlineData(MergeStatus.NotStarted)]
    [InlineData(MergeStatus.Queued)]
    [InlineData(MergeStatus.Converting)]
    [InlineData(MergeStatus.Merging)]
    public async Task Submit_BlockedWith409_WhileMergeUnfinished(MergeStatus status)
    {
        await using var db = CreateDbContext();
        var inspectionId = await SeedAsync(db, status);
        var service = CreateService(db);

        var (response, statusCode, error) = await service.SubmitQpadmResultAsync(inspectionId, NewRequest());

        Assert.Null(response);
        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        Assert.Contains("merge", error, StringComparison.OrdinalIgnoreCase);
        Assert.False(await db.QpadmResults.AnyAsync()); // nothing persisted
    }

    [Fact]
    public async Task Submit_Blocked_WhenMergeFailed_WithFailureMessage()
    {
        await using var db = CreateDbContext();
        var inspectionId = await SeedAsync(db, MergeStatus.Failed);
        var service = CreateService(db);

        var (_, statusCode, error) = await service.SubmitQpadmResultAsync(inspectionId, NewRequest());

        Assert.Equal(StatusCodes.Status409Conflict, statusCode);
        Assert.Contains("failed", error, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(MergeStatus.Ready)]
    [InlineData(MergeStatus.Deleted)] // edits after completion: the merge did finish, just got cleaned up
    public async Task Submit_Allowed_WhenMergeFinished(MergeStatus status)
    {
        await using var db = CreateDbContext();
        var inspectionId = await SeedAsync(db, status);
        var service = CreateService(db);

        var (response, statusCode, _) = await service.SubmitQpadmResultAsync(inspectionId, NewRequest());

        Assert.Equal(StatusCodes.Status201Created, statusCode);
        Assert.NotNull(response);
        Assert.True(await db.QpadmResults.AnyAsync());
    }

    [Fact]
    public async Task Submit_NotFound_WhenInspectionMissing()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var (_, statusCode, _) = await service.SubmitQpadmResultAsync(999, NewRequest());

        Assert.Equal(StatusCodes.Status404NotFound, statusCode);
    }

    // ── helpers ───────────────────────────────────────────────────────────
    private static SubmitQpadmResultContract.Request NewRequest() =>
        new() { EraGroups = [], OrderStatus = nameof(OrderStatus.InProcess) };

    private static GeneticInspectionService CreateService(ApplicationDbContext db) =>
        new(db, new StubNotificationService(), new NoopJobClient(), new NoopRealtimeNotifier(),
            NullLogger<GeneticInspectionService>.Instance);

    private static async Task<int> SeedAsync(ApplicationDbContext db, MergeStatus mergeStatus)
    {
        var now = DateTime.UtcNow;
        // The submit query inner-joins the required User relationship, so the owner must exist.
        var user = new User { IdentityId = "auth0|t", Username = "t", Email = "t@t.io", CreatedBy = "t", CreatedAt = now, UpdatedAt = now };
        db.Users.Add(user);

        var rawFile = new RawGeneticFile
        {
            RawDataFileName = "sample.txt",
            RawData = "rs1\t1\t100\tAG\n"u8.ToArray(),
            MergeStatus = mergeStatus,
            MergeId = mergeStatus is MergeStatus.Ready or MergeStatus.Deleted ? "insp-1-abc" : null,
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
            UserId = user.Id,
            OrderId = order.Id,
            CreatedBy = "t",
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.QpadmGeneticInspections.Add(inspection);
        await db.SaveChangesAsync();
        return inspection.Id;
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"submit-guard-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options, new Odin.Api.Authentication.RequestAppContext());
    }

    private sealed class StubNotificationService : INotificationService
    {
        public Task CreateAndSendAsync(int recipientUserId, NotificationType type, string title, string message,
            string? referenceId = null) => Task.CompletedTask;
        public Task<List<GetNotificationContract.Response>> GetNotificationsAsync(int userId, int page, int pageSize)
            => Task.FromResult(new List<GetNotificationContract.Response>());
        public Task<int> GetUnreadCountAsync(int userId) => Task.FromResult(0);
        public Task MarkAllAsReadAsync(int userId) => Task.CompletedTask;
    }

    private sealed class NoopJobClient : IBackgroundJobClient
    {
        public string Create(Job job, IState state) => Guid.NewGuid().ToString("N");
        public bool ChangeState(string jobId, IState state, string expectedState) => true;
    }

    private sealed class NoopRealtimeNotifier : IGeneticInspectionRealtimeNotifier
    {
        public Task NotifyChangedAsync(string reason, int? inspectionId = null,
            CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
