using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Odin.Api.Data;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.HaplogroupHeatmap;
using Odin.Api.Endpoints.HaplogroupHeatmap.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.HaplogroupHeatmap;

/// <summary>
/// Exercises the Y-haplogroup heatmap end-to-end against the container Postgres: the rerunnable import
/// (idempotent reload) and the distribution endpoint's recursive-CTE subtree/migration queries.
/// The import is driven by a fake export client so no real odin-tools-api is needed.
/// </summary>
public class HaplogroupHeatmapTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // tree: R -> R-M269 -> {R-U106, R-P312}; I (separate clade, must be excluded from R-M269's subtree)
    private static List<HaploGeoNodeDto> Nodes() =>
    [
        new("R", null, 22000, 24000, "M207", 50.0, 30.0, 4),
        new("R-M269", "R", 6400, 13300, "M269", 51.0, 8.0, 3),
        new("R-U106", "R-M269", 4900, 5000, "U106", 52.5, 2.0, 1),
        new("R-P312", "R-M269", 4900, 5000, "P312", 48.0, 1.0, 1),
        new("I", null, 27000, 28000, "M170", 45.0, 20.0, 1),
    ];

    private static List<HaploGeoSampleDto> Samples() =>
    [
        new("a1", "a1", "R-M269", "R-M269", null, null, 48.0, 20.0, 4500, null, null, "BronzeAge", "ancient", "Hungary", null, null, "M", "PASS"),
        new("a2", "a2", "R-U106", "R-U106", null, null, 52.5, 2.0, 2200, null, null, "IronAge", "ancient", "Germany", null, null, "M", "PASS"),
        new("m1", "m1", "R-P312", "R-P312", null, null, 48.1, 1.2, 0, null, null, "Modern", "modern", "France", null, null, "M", "PASS"),
        new("m2", "m2", "R-M269", "R-M269", null, null, 47.9, 19.8, 0, null, null, "Modern", "modern", "Hungary", null, null, "M", "PASS"),
        new("i1", "i1", "I", "I", null, null, 45.0, 20.0, 7000, null, null, "Neolithic", "ancient", "Serbia", null, null, "M", "PASS"),
    ];

    /// <summary>Returns the fixed fake dataset, paginated exactly like the real export.</summary>
    private sealed class FakeExportClient : IHaploGeoExportClient
    {
        private readonly List<HaploGeoNodeDto> _nodes = Nodes();
        private readonly List<HaploGeoSampleDto> _samples = Samples();

        public Task<HaploGeoExportMeta> GetMetaAsync(CancellationToken ct = default) =>
            Task.FromResult(new HaploGeoExportMeta("v-test", _samples.Count, _nodes.Count, 0, 0));

        public Task<HaploGeoPage<HaploGeoSampleDto>> GetSamplesAsync(int offset, int limit, CancellationToken ct = default) =>
            Task.FromResult(new HaploGeoPage<HaploGeoSampleDto>(offset, limit, _samples.Count, _samples.Skip(offset).Take(limit).ToList()));

        public Task<HaploGeoPage<HaploGeoNodeDto>> GetNodesAsync(int offset, int limit, CancellationToken ct = default) =>
            Task.FromResult(new HaploGeoPage<HaploGeoNodeDto>(offset, limit, _nodes.Count, _nodes.Skip(offset).Take(limit).ToList()));

        public Task<HaploGeoPage<HaploGeoFrequencyDto>> GetFrequenciesAsync(int offset, int limit, CancellationToken ct = default) =>
            Task.FromResult(new HaploGeoPage<HaploGeoFrequencyDto>(offset, limit, 0, []));
    }

    private async Task RunImportAsync()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var service = new HaplogroupImportService(db, new FakeExportClient(), cache, NullLogger<HaplogroupImportService>.Instance);
        await service.ImportAsync("integration-test");
    }

    /// <summary>Records the clade it's asked for and returns a canned grid — the real grid is computed by
    /// the tools-api, which isn't running in tests.</summary>
    private sealed class FakeRelativeFrequencyClient : IHaplogroupRelativeFrequencyClient
    {
        public string? RequestedClade { get; private set; }
        public string? RequestedLayer { get; private set; }

        public Task<HaploGeoRelativeFrequencyDto> GetAsync(
            string clade, string layer, double radiusKm, CancellationToken cancellationToken = default)
        {
            RequestedClade = clade;
            RequestedLayer = layer;
            return Task.FromResult(new HaploGeoRelativeFrequencyDto(
                Clade: clade, Layer: layer, RadiusKm: radiusKm, CellSize: 1.0,
                FrequencyClade: layer == "modern" ? clade : null, MaxValue: 75.0, CladeCount: 2, TotalCount: 4,
                Cells: [new HaploGeoRfCellDto(48.0, 20.0, 75.0)]));
        }
    }

    [Fact]
    public async Task Import_PopulatesTables_AndIsIdempotentOnRerun()
    {
        await RunImportAsync();
        await RunImportAsync(); // re-run must not duplicate rows (delete-and-reload)

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(5, await db.YHaplogroupTreeNodes.CountAsync());
        Assert.Equal(5, await db.YHaplogroupSamples.CountAsync());

        var runs = await db.HaplogroupImportRuns.OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(2, runs.Count);
        Assert.All(runs, r => Assert.Equal(HaplogroupImportStatus.Completed, r.Status));
        Assert.All(runs, r => Assert.Equal("v-test", r.DatasetVersion));
    }

    [Fact]
    public async Task Distribution_AggregatesSubtree_ExcludesOtherClades_AndBuildsMigration()
    {
        await RunImportAsync();

        var body = await Client.GetFromJsonAsync<HaplogroupDistributionContract.Response>(
            "/api/clade-finder/distribution?clade=R-M269");

        Assert.NotNull(body);
        Assert.True(body!.Found);
        // R-M269 is itself a named subclade, so the heatmap anchors on it (not the bare letter).
        Assert.Equal("R-M269", body.DisplayClade);
        // R-M269 subtree = {R-M269, R-U106, R-P312}: 2 ancient (a1, a2) + 2 modern (m1, m2). The 'I' sample is excluded.
        Assert.Equal(2, body.TotalAncient);
        Assert.Equal(2, body.TotalModern);
        Assert.Contains(body.Ancient, b => b.Era == "BronzeAge");
        Assert.Contains(body.Ancient, b => b.Era == "IronAge");
        Assert.DoesNotContain(body.ModernByCountry, c => c.Country == "Serbia");
        Assert.Contains(body.ModernByCountry, c => c.Country is "France" or "Hungary");

        // Migration = ancestor chain of R-M269 ordered oldest-first: R (22000) then R-M269 (6400).
        Assert.Equal(2, body.Migration.Count);
        Assert.Equal("R", body.Migration[0].Clade);
        Assert.Equal("R-M269", body.Migration[^1].Clade);
        Assert.True(body.Migration[0].Tmrca >= body.Migration[^1].Tmrca);
    }

    [Fact]
    public async Task RelativeFrequency_AnchorsClade_ProxiesGrid_AndMapsResponse()
    {
        await RunImportAsync();

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var fakeClient = new FakeRelativeFrequencyClient();
        var service = new HaplogroupRelativeFrequencyService(db, fakeClient, cache, env);

        var response = await service.GetAsync("R-M269", "modern", 99999); // radius is clamped to the max

        Assert.True(response.Found);
        Assert.Equal("R-M269", response.DisplayClade); // R-M269 is a named subclade → anchors to itself
        Assert.Equal("R-M269", fakeClient.RequestedClade); // the anchored clade is what the grid is asked for
        Assert.Equal("modern", fakeClient.RequestedLayer);
        Assert.Equal(2000.0, response.RadiusKm); // clamped
        Assert.Equal("R-M269", response.FrequencyClade);
        Assert.Equal(75.0, response.MaxValue);
        Assert.Single(response.Cells);
    }

    [Fact]
    public async Task RelativeFrequency_UnknownClade_ReturnsNotFound_WithoutCallingGrid()
    {
        await RunImportAsync();

        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var cache = scope.ServiceProvider.GetRequiredService<IMemoryCache>();
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        var fakeClient = new FakeRelativeFrequencyClient();
        var service = new HaplogroupRelativeFrequencyService(db, fakeClient, cache, env);

        var response = await service.GetAsync("Z-DOESNOTEXIST", "ancient", 300);

        Assert.False(response.Found);
        Assert.Empty(response.Cells);
        Assert.Null(fakeClient.RequestedClade); // never proxied for an unknown clade
    }

    [Fact]
    public async Task Distribution_UnknownClade_ReturnsNotFoundFlagWithEmptyData()
    {
        await RunImportAsync();

        var body = await Client.GetFromJsonAsync<HaplogroupDistributionContract.Response>(
            "/api/clade-finder/distribution?clade=Z-DOESNOTEXIST");

        Assert.NotNull(body);
        Assert.False(body!.Found);
        Assert.Empty(body.Ancient);
        Assert.Empty(body.Migration);
    }
}
