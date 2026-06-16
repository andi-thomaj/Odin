using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.CladeFinderManagement;
using Odin.Api.Endpoints.CladeFinderManagement.Models;
using Odin.Api.Endpoints.OrderManagement;
using Odin.Api.Endpoints.OrderManagement.Models;

namespace Odin.Api.Tests.Endpoints.CladeFinderManagement;

public class YHaplogroupComputeServiceTests
{
    [Fact]
    public async Task ComputeAndPersist_Male_WithCladeResult_PersistsCompleted()
    {
        await using var db = CreateDbContext();
        var inspectionId = await SeedInspectionAsync(db, Gender.Male);

        var response = new AnalyzeCladeContract.Response
        {
            Clade = "J-Z1865",
            Score = 12.0,
            NextPrediction = new AnalyzeCladeContract.NextPrediction { Clade = "J-P58", Score = 11.0 },
            Downstream = [new AnalyzeCladeContract.DownstreamClade { Clade = "J-Y4000", Children = 3 }],
            Lineage = ["J", "J-P58", "J-Z1865"],
            PositivesUsed = 40,
            NegativesUsed = 8,
            YReads = 1200,
            SourceFormat = "microarray",
            EffectiveBuild = "hg19",
        };
        var service = CreateService(db, StubReturning(response));

        await service.ComputeAndPersistAsync(inspectionId);

        var record = await db.QpadmCladeResults.SingleAsync(r => r.GeneticInspectionId == inspectionId);
        Assert.Equal(CladeAnalysisStatus.Completed, record.Status);
        Assert.Equal("J-Z1865", record.Clade);
        Assert.Equal("J-P58", record.NextPredictionClade);
        Assert.Equal(11.0, record.NextPredictionScore);
        Assert.Equal(["J", "J-P58", "J-Z1865"], record.Lineage);
        Assert.Single(record.Downstream);
        Assert.Equal("hg19", record.EffectiveBuild);
        Assert.Null(record.Message);
    }

    [Fact]
    public async Task ComputeAndPersist_Male_NoYData_PersistsNoYDataWithFriendlyMessage()
    {
        await using var db = CreateDbContext();
        var inspectionId = await SeedInspectionAsync(db, Gender.Male);
        var service = CreateService(db, StubThrowing(new CladeFinderException(
            HttpStatusCode.BadRequest, "No Y-chromosome SNP calls were found in the upload.")));

        await service.ComputeAndPersistAsync(inspectionId);

        var record = await db.QpadmCladeResults.SingleAsync(r => r.GeneticInspectionId == inspectionId);
        Assert.Equal(CladeAnalysisStatus.NoYData, record.Status);
        Assert.Null(record.Clade);
        Assert.Contains("No Y-chromosome markers", record.Message);
    }

    [Fact]
    public async Task ComputeAndPersist_Male_OtherBadRequest_PersistsInvalidDataWithDetail()
    {
        await using var db = CreateDbContext();
        var inspectionId = await SeedInspectionAsync(db, Gender.Male);
        const string detail = "Found 5 Y-chromosome reads, but none matched the reference markers.";
        var service = CreateService(db, StubThrowing(new CladeFinderException(HttpStatusCode.BadRequest, detail)));

        await service.ComputeAndPersistAsync(inspectionId);

        var record = await db.QpadmCladeResults.SingleAsync(r => r.GeneticInspectionId == inspectionId);
        Assert.Equal(CladeAnalysisStatus.InvalidData, record.Status);
        Assert.Equal(detail, record.Message);
    }

    [Fact]
    public async Task ComputeAndPersist_Male_ServiceUnavailable_PersistsUnavailable_AndRethrows()
    {
        await using var db = CreateDbContext();
        var inspectionId = await SeedInspectionAsync(db, Gender.Male);
        var service = CreateService(db, StubThrowing(new CladeFinderException(
            HttpStatusCode.ServiceUnavailable, "reference data not configured")));

        await Assert.ThrowsAsync<CladeFinderException>(() => service.ComputeAndPersistAsync(inspectionId));

        var record = await db.QpadmCladeResults.SingleAsync(r => r.GeneticInspectionId == inspectionId);
        Assert.Equal(CladeAnalysisStatus.Unavailable, record.Status);
    }

    [Fact]
    public async Task ComputeAndPersist_Female_SkipsServiceCall_PersistsNotApplicable()
    {
        await using var db = CreateDbContext();
        var inspectionId = await SeedInspectionAsync(db, Gender.Female);
        var called = false;
        var service = CreateService(db, new StubCladeFinder((_, _, _) =>
        {
            called = true;
            throw new InvalidOperationException("clade service must not be called for a female kit");
        }));

        await service.ComputeAndPersistAsync(inspectionId);

        Assert.False(called);
        var record = await db.QpadmCladeResults.SingleAsync(r => r.GeneticInspectionId == inspectionId);
        Assert.Equal(CladeAnalysisStatus.NotApplicable, record.Status);
        Assert.Contains("paternal line", record.Message);
    }

    [Fact]
    public async Task ComputeAndPersist_AlreadyCompleted_IsNoOp()
    {
        await using var db = CreateDbContext();
        var inspectionId = await SeedInspectionAsync(db, Gender.Male);
        db.QpadmCladeResults.Add(new QpadmCladeResult
        {
            GeneticInspectionId = inspectionId,
            Status = CladeAnalysisStatus.Completed,
            Clade = "R-M269",
            ResultsVersion = "v1",
            CreatedBy = "t",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var service = CreateService(db, new StubCladeFinder((_, _, _) =>
            throw new InvalidOperationException("must not re-run a completed analysis")));

        await service.ComputeAndPersistAsync(inspectionId);

        var record = await db.QpadmCladeResults.SingleAsync(r => r.GeneticInspectionId == inspectionId);
        Assert.Equal("R-M269", record.Clade);
    }

    [Fact]
    public async Task ComputeAndPersist_RemovesCachedQpadmResultForOrder()
    {
        await using var db = CreateDbContext();
        var inspectionId = await SeedInspectionAsync(db, Gender.Male);
        var orderId = (await db.QpadmGeneticInspections.AsNoTracking().FirstAsync(i => i.Id == inspectionId)).OrderId;

        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(OrderResultCacheKeys.Qpadm(orderId),
            new GetOrderQpadmResultContract.Response { FirstName = "STALE" });

        var response = new AnalyzeCladeContract.Response { Clade = "R-M269", Score = 9.0, Lineage = ["R", "R-M269"] };
        var service = CreateService(db, StubReturning(response), cache);

        await service.ComputeAndPersistAsync(inspectionId);

        // Persisting a fresh clade result must evict the cached qpAdm payload so the next view reflects it.
        Assert.False(cache.TryGetValue(OrderResultCacheKeys.Qpadm(orderId), out _));
    }

    [Fact]
    public async Task ComputeAndPersist_AlreadyCompleted_LeavesCacheIntact()
    {
        await using var db = CreateDbContext();
        var inspectionId = await SeedInspectionAsync(db, Gender.Male);
        var orderId = (await db.QpadmGeneticInspections.AsNoTracking().FirstAsync(i => i.Id == inspectionId)).OrderId;
        db.QpadmCladeResults.Add(new QpadmCladeResult
        {
            GeneticInspectionId = inspectionId,
            Status = CladeAnalysisStatus.Completed,
            Clade = "R-M269",
            ResultsVersion = "v1",
            CreatedBy = "t",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set(OrderResultCacheKeys.Qpadm(orderId),
            new GetOrderQpadmResultContract.Response { FirstName = "KEEP" });
        var service = CreateService(db, new StubCladeFinder((_, _, _) =>
            throw new InvalidOperationException("must not re-run a completed analysis")), cache);

        await service.ComputeAndPersistAsync(inspectionId);

        // The already-Completed short-circuit makes no DB change, so it must not evict an unrelated entry.
        Assert.True(cache.TryGetValue(OrderResultCacheKeys.Qpadm(orderId),
            out GetOrderQpadmResultContract.Response? kept));
        Assert.Equal("KEEP", kept!.FirstName);
    }

    // ── helpers ───────────────────────────────────────────────────────────
    private static YHaplogroupComputeService CreateService(
        ApplicationDbContext db, ICladeFinderService clade, IMemoryCache? cache = null)
        => new(db, clade, cache ?? new MemoryCache(new MemoryCacheOptions()),
            new Odin.Api.Authentication.RequestAppContext(),
            NullLogger<YHaplogroupComputeService>.Instance);

    private static ICladeFinderService StubReturning(AnalyzeCladeContract.Response response)
        => new StubCladeFinder((_, _, _) => Task.FromResult(response));

    private static ICladeFinderService StubThrowing(Exception ex)
        => new StubCladeFinder((_, _, _) => throw ex);

    private static async Task<int> SeedInspectionAsync(ApplicationDbContext db, Gender gender)
    {
        var now = DateTime.UtcNow;
        var rawFile = new RawGeneticFile
        {
            RawDataFileName = "sample.txt",
            RawData = "rs1\tY\t1001\tT\n"u8.ToArray(),
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
            Gender = gender,
            RawGeneticFileId = rawFile.Id,
            UserId = 1,
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
            .UseInMemoryDatabase($"ydna-tests-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options, new Odin.Api.Authentication.RequestAppContext());
    }

    private sealed class StubCladeFinder(
        Func<IFormFile, string?, CancellationToken, Task<AnalyzeCladeContract.Response>> impl) : ICladeFinderService
    {
        public Task<AnalyzeCladeContract.Response> AnalyzeAsync(
            IFormFile file, string? build, CancellationToken cancellationToken = default)
            => impl(file, build, cancellationToken);
    }
}
