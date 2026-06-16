using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.G25Calculations;
using Odin.Api.Endpoints.G25Calculations.Models;
using Odin.Api.Endpoints.OrderManagement;
using Odin.Api.Endpoints.OrderManagement.Models;
using Odin.Api.Hubs;
using Odin.Api.Services;

namespace Odin.Api.Tests.Endpoints.OrderManagement;

/// <summary>
/// Unit tests for the 5-day backend result cache added to <see cref="OrderService"/> for the Ancient
/// Origins qpAdm/G25 result views. The cache is deliberately bypassed under the "Testing" host
/// environment (mirroring <c>EraService</c>) to avoid cross-test pollution in the integration suite,
/// so these tests run the service under a non-Testing environment to exercise the real cache paths.
/// </summary>
public class OrderServiceCacheTests
{
    private const string Owner = "auth0|owner";
    private const string Intruder = "auth0|intruder";

    // ── qpAdm: serves from cache after auth, and never before it ────────────

    [Fact]
    public async Task GetQpadmResult_WhenCached_ReturnsCachedPayload()
    {
        await using var db = CreateDbContext();
        var cache = NewCache();
        var orderId = await SeedQpadmOrderAsync(db, OrderStatus.Completed, Owner, withInspection: true);
        cache.Set(OrderResultCacheKeys.Qpadm(orderId),
            new GetOrderQpadmResultContract.Response { FirstName = "FROM-CACHE" });

        var service = CreateService(db, cache);
        var (result, status, _) = await service.GetQpadmResultForOrderAsync(orderId, Owner);

        Assert.Equal(200, status);
        Assert.Equal("FROM-CACHE", result!.FirstName);
    }

    [Fact]
    public async Task GetQpadmResult_EnforcesOwnership_BeforeReadingCache()
    {
        await using var db = CreateDbContext();
        var cache = NewCache();
        var orderId = await SeedQpadmOrderAsync(db, OrderStatus.Completed, Owner, withInspection: true);
        cache.Set(OrderResultCacheKeys.Qpadm(orderId),
            new GetOrderQpadmResultContract.Response { FirstName = "FROM-CACHE" });

        var service = CreateService(db, cache);
        var (result, status, _) = await service.GetQpadmResultForOrderAsync(orderId, Intruder, isAdmin: false);

        Assert.Equal(403, status);
        Assert.Null(result); // The cached payload must never leak to a non-owner.
    }

    [Fact]
    public async Task GetQpadmResult_EnforcesCompletedStatus_BeforeReadingCache()
    {
        await using var db = CreateDbContext();
        var cache = NewCache();
        var orderId = await SeedQpadmOrderAsync(db, OrderStatus.Pending, Owner, withInspection: true);
        cache.Set(OrderResultCacheKeys.Qpadm(orderId),
            new GetOrderQpadmResultContract.Response { FirstName = "FROM-CACHE" });

        var service = CreateService(db, cache);
        var (result, status, _) = await service.GetQpadmResultForOrderAsync(orderId, Owner);

        Assert.Equal(400, status); // Pending orders are rejected even though an entry is cached.
        Assert.Null(result);
    }

    // ── G25: serves from cache, populates on miss, honors gates ─────────────

    [Fact]
    public async Task GetG25Result_WhenCached_ReturnsCachedPayload()
    {
        await using var db = CreateDbContext();
        var cache = NewCache();
        var orderId = await SeedG25OrderAsync(db, OrderStatus.Completed, Owner);
        cache.Set(OrderResultCacheKeys.G25(orderId),
            new GetOrderG25ResultContract.Response { FirstName = "FROM-CACHE" });

        var service = CreateService(db, cache);
        var (result, status, _) = await service.GetG25ResultForOrderAsync(orderId, Owner);

        Assert.Equal(200, status);
        Assert.Equal("FROM-CACHE", result!.FirstName);
    }

    [Fact]
    public async Task GetG25Result_EnforcesOwnership_BeforeReadingCache()
    {
        await using var db = CreateDbContext();
        var cache = NewCache();
        var orderId = await SeedG25OrderAsync(db, OrderStatus.Completed, Owner);
        cache.Set(OrderResultCacheKeys.G25(orderId),
            new GetOrderG25ResultContract.Response { FirstName = "FROM-CACHE" });

        var service = CreateService(db, cache);
        var (result, status, _) = await service.GetG25ResultForOrderAsync(orderId, Intruder, isAdmin: false);

        Assert.Equal(403, status);
        Assert.Null(result);
    }

    [Fact]
    public async Task GetG25Result_OnCacheMiss_PopulatesCache_ForCompletedOrderWithResults()
    {
        await using var db = CreateDbContext();
        var cache = NewCache();
        var orderId = await SeedG25OrderAsync(db, OrderStatus.Completed, Owner, withDistanceResult: true);

        var service = CreateService(db, cache);

        Assert.False(cache.TryGetValue(OrderResultCacheKeys.G25(orderId), out _));
        var (result, status, _) = await service.GetG25ResultForOrderAsync(orderId, Owner);

        Assert.Equal(200, status);
        Assert.NotNull(result);
        // The expensive payload is now cached under the namespaced key for the next view.
        Assert.True(cache.TryGetValue(OrderResultCacheKeys.G25(orderId), out _));
    }

    [Fact]
    public async Task GetG25Result_DoesNotCache_WhenOrderHasNoDistanceResults()
    {
        await using var db = CreateDbContext();
        var cache = NewCache();
        var orderId = await SeedG25OrderAsync(db, OrderStatus.Completed, Owner, withInspection: true);

        var service = CreateService(db, cache);
        var (_, status, _) = await service.GetG25ResultForOrderAsync(orderId, Owner);

        Assert.Equal(200, status);
        // An empty (still-processing) result must not be frozen for 5 days.
        Assert.False(cache.TryGetValue(OrderResultCacheKeys.G25(orderId), out _));
    }

    [Fact]
    public async Task GetG25Result_DoesNotCache_WhenOrderNotCompleted()
    {
        await using var db = CreateDbContext();
        var cache = NewCache();
        var orderId = await SeedG25OrderAsync(db, OrderStatus.InProcess, Owner, withDistanceResult: true);

        var service = CreateService(db, cache);
        var (_, status, _) = await service.GetG25ResultForOrderAsync(orderId, Owner);

        Assert.Equal(200, status);
        Assert.False(cache.TryGetValue(OrderResultCacheKeys.G25(orderId), out _));
    }

    // ── Environment gate: caching is bypassed under "Testing" ───────────────

    [Fact]
    public async Task GetG25Result_UnderTestingEnvironment_BypassesCache()
    {
        await using var db = CreateDbContext();
        var cache = NewCache();
        // Order exists (auth passes) but has no genetic inspection.
        var orderId = await SeedG25OrderAsync(db, OrderStatus.Completed, Owner);
        cache.Set(OrderResultCacheKeys.G25(orderId),
            new GetOrderG25ResultContract.Response { FirstName = "FROM-CACHE" });

        var service = CreateService(db, cache, FakeHostEnvironment.Testing());
        var (result, status, _) = await service.GetG25ResultForOrderAsync(orderId, Owner);

        // Cache is ignored, so it falls through to the DB and 404s on the missing inspection
        // instead of returning the seeded cache entry.
        Assert.Equal(404, status);
        Assert.Null(result);
    }

    // ── Invalidation ────────────────────────────────────────────────────────

    [Fact]
    public async Task DeleteAsync_RemovesCachedQpadmResult()
    {
        await using var db = CreateDbContext();
        var cache = NewCache();
        var orderId = await SeedQpadmOrderAsync(db, OrderStatus.Completed, Owner, withInspection: false);
        cache.Set(OrderResultCacheKeys.Qpadm(orderId),
            new GetOrderQpadmResultContract.Response { FirstName = "FROM-CACHE" });

        var service = CreateService(db, cache);
        var deleted = await service.DeleteAsync(orderId);

        Assert.True(deleted);
        Assert.False(cache.TryGetValue(OrderResultCacheKeys.Qpadm(orderId), out _));
    }

    // ── Direct unit tests for the cache-key helper and the qpAdm guard ──────

    [Fact]
    public void CacheKeys_AreNamespacedByService_SoCollidingIdsDoNotClash()
    {
        Assert.Equal("order-result:qpadm:7", OrderResultCacheKeys.Qpadm(7));
        Assert.Equal("order-result:g25:7", OrderResultCacheKeys.G25(7));
        Assert.NotEqual(OrderResultCacheKeys.Qpadm(7), OrderResultCacheKeys.G25(7));
    }

    [Theory]
    [InlineData("Completed", true)]
    [InlineData("NoYData", true)]
    [InlineData("InvalidData", true)]
    [InlineData("NotApplicable", true)]
    [InlineData("Pending", false)]      // backfill just enqueued — will change on a later view
    [InlineData("Unavailable", false)]  // transient failure — self-heals on a later view
    public void IsQpadmResponseCacheable_OnlyTerminalYDnaStatusesAreCacheable(string status, bool expected)
    {
        var response = new GetOrderQpadmResultContract.Response
        {
            YDna = new GetOrderQpadmResultContract.YDnaResult { Status = status }
        };

        Assert.Equal(expected, OrderService.IsQpadmResponseCacheable(response));
    }

    [Fact]
    public void IsQpadmResponseCacheable_IsFalse_WhenYDnaMissing()
    {
        var response = new GetOrderQpadmResultContract.Response { YDna = null };
        Assert.False(OrderService.IsQpadmResponseCacheable(response));
    }

    // ── helpers ─────────────────────────────────────────────────────────────

    private static MemoryCache NewCache() => new(new MemoryCacheOptions());

    private static OrderService CreateService(
        ApplicationDbContext db, IMemoryCache cache, IHostEnvironment? env = null)
        => new(
            db,
            new StubGeoLocationService(),
            new StubG25CalculationService(),
            new StubBackgroundJobClient(),
            Options.Create(new OrderLimitsOptions()),
            cache,
            new NoopRealtimeNotifier(),
            env ?? FakeHostEnvironment.Production(),
            NullLogger<OrderService>.Instance);

    private static async Task<int> SeedQpadmOrderAsync(
        ApplicationDbContext db, OrderStatus status, string owner, bool withInspection)
    {
        var now = DateTime.UtcNow;
        var order = new QpadmOrder { Status = status, CreatedBy = owner, CreatedAt = now, UpdatedAt = now };
        db.QpadmOrders.Add(order);
        await db.SaveChangesAsync();

        if (withInspection)
        {
            db.QpadmGeneticInspections.Add(new QpadmGeneticInspection
            {
                FirstName = "Test",
                MiddleName = "",
                LastName = "User",
                Gender = Gender.Male,
                RawGeneticFileId = 1,
                UserId = 1,
                OrderId = order.Id,
                CreatedBy = owner,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        return order.Id;
    }

    private static async Task<int> SeedG25OrderAsync(
        ApplicationDbContext db, OrderStatus status, string owner,
        bool withInspection = false, bool withDistanceResult = false)
    {
        var now = DateTime.UtcNow;
        var order = new G25Order { Status = status, CreatedBy = owner, CreatedAt = now, UpdatedAt = now };
        db.G25Orders.Add(order);
        await db.SaveChangesAsync();

        if (withInspection || withDistanceResult)
        {
            var inspection = new G25GeneticInspection
            {
                FirstName = "Test",
                MiddleName = "",
                LastName = "User",
                RawGeneticFileId = 1,
                UserId = 1,
                OrderId = order.Id,
            };
            db.G25GeneticInspections.Add(inspection);
            await db.SaveChangesAsync();

            if (withDistanceResult)
            {
                var era = new G25DistanceEra { Name = "Bronze Age", CreatedBy = owner, CreatedAt = now, UpdatedAt = now };
                db.G25DistanceEras.Add(era);
                await db.SaveChangesAsync();

                db.G25DistanceResults.Add(new G25DistanceResult
                {
                    GeneticInspectionId = inspection.Id,
                    G25DistanceEraId = era.Id,
                    ResultsVersion = "v1",
                    Populations = [],
                    CreatedBy = owner,
                    CreatedAt = now,
                    UpdatedAt = now,
                });
                await db.SaveChangesAsync();
            }
        }

        return order.Id;
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"order-cache-tests-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options, new Odin.Api.Authentication.RequestAppContext());
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Production";
        public string ApplicationName { get; set; } = "Odin.Api.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = null!;

        public static FakeHostEnvironment Production() => new() { EnvironmentName = "Production" };
        public static FakeHostEnvironment Testing() => new() { EnvironmentName = "Testing" };
    }

    private sealed class StubGeoLocationService : IGeoLocationService
    {
        public Task<GeoLocationResult?> GetCountryFromIpAsync(string? ipAddress)
            => Task.FromResult<GeoLocationResult?>(null);
    }

    private sealed class StubG25CalculationService : IG25CalculationService
    {
        public Task<(ComputeDistancesContract.Response? Response, string? Error, bool NotFound)> ComputeDistancesAsync(
            ComputeDistancesContract.Request request, CancellationToken ct = default)
            => Task.FromResult<(ComputeDistancesContract.Response?, string?, bool)>((null, null, false));

        public Task<(ComputeAdmixtureSingleContract.Response? Response, string? Error, bool NotFound)> ComputeAdmixtureSingleAsync(
            ComputeAdmixtureSingleContract.Request request, CancellationToken ct = default)
            => Task.FromResult<(ComputeAdmixtureSingleContract.Response?, string?, bool)>((null, null, false));

        public Task<(ComputeAdmixtureMultiContract.Response? Response, string? Error)> ComputeAdmixtureMultiAsync(
            ComputeAdmixtureMultiContract.Request request, CancellationToken ct = default)
            => Task.FromResult<(ComputeAdmixtureMultiContract.Response?, string?)>((null, null));
    }

    private sealed class StubBackgroundJobClient : IBackgroundJobClient
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
