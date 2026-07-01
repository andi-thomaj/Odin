using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25PcaScatter.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.G25PcaScatter;

/// <summary>
/// End-to-end coverage of <c>GET /v1/api/g25-pca/eras/{eraId}</c> against the AGGREGATED PCA model:
/// each <c>g25_pca_populations_samples</c> row is ONE population (a cluster) whose <c>Coordinates</c>
/// holds every member individual's 25-value group joined with ';'. The endpoint must expand a single row
/// into one plotted point per member (all tagged with the row's label), fit the per-era PCA over the
/// flattened cloud, and return one centroid per population. Caching is skipped under the Testing host
/// env, so each test reads exactly what it seeds.
/// </summary>
public class G25PcaScatterEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const int Dims = 25;

    // A single member individual's 25-value coordinate group. Only the first three PCs vary (enough
    // spread for a stable 2-axis fit); the rest are zero.
    private static string Member(double a, double b, double c)
    {
        var v = new double[Dims];
        v[0] = a;
        v[1] = b;
        v[2] = c;
        return string.Join(',', v.Select(x => x.ToString(CultureInfo.InvariantCulture)));
    }

    // A population's aggregated Coordinates string: `count` member groups joined with ';', spread across
    // three PCs (co-prime moduli keep the cloud full-rank so lambda2 > 0 and the basis fits).
    private static string Cluster(int count, int offset = 0)
    {
        var members = new string[count];
        for (var i = 0; i < count; i++)
        {
            var t = i + offset;
            members[i] = Member(
                0.10 + 0.03 * (t % 5),
                0.05 + 0.02 * (t % 4),
                0.01 + 0.015 * (t % 3));
        }

        return string.Join(';', members);
    }

    private async Task<int> SeedPcaEraAsync(params (string Label, string Coordinates)[] populations)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        var era = new G25DistanceEra
        {
            Name = $"Era-{Guid.NewGuid():N}",
            CreatedAt = now, CreatedBy = "test", UpdatedAt = now, UpdatedBy = "test"
        };
        db.G25DistanceEras.Add(era);
        await db.SaveChangesAsync();

        foreach (var (label, coordinates) in populations)
        {
            db.G25PcaPopulationsSamples.Add(new G25PcaPopulationsSample
            {
                Label = label,
                Coordinates = coordinates,
                Ids = string.Empty,
                G25DistanceEraId = era.Id,
                CreatedAt = now, CreatedBy = "test", UpdatedAt = now, UpdatedBy = "test"
            });
        }

        await db.SaveChangesAsync();
        return era.Id;
    }

    [Fact]
    public async Task EraScatter_ExpandsEachRowIntoItsPopulationCluster()
    {
        // Two population rows, 6 members each → 12 individual points across 2 clusters.
        var eraId = await SeedPcaEraAsync(
            ("Alpha", Cluster(6, offset: 0)),
            ("Beta", Cluster(6, offset: 3)));

        var response = await Client.GetAsync($"/api/g25-pca/eras/{eraId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scatter = await response.Content.ReadFromJsonAsync<PcaEraScatterContract.Response>();
        Assert.NotNull(scatter);
        Assert.Equal(eraId, scatter!.EraId);

        // The two rows expanded into one plotted point per member individual.
        Assert.Equal(12, scatter.TotalSamples);
        Assert.Equal(12, scatter.PlottedSamples);
        Assert.Equal(12, scatter.Points.Count);

        // Every point carries its population's label; the members split 6/6 by cluster.
        var byLabel = scatter.Points.GroupBy(p => p.Label).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(6, byLabel["Alpha"]);
        Assert.Equal(6, byLabel["Beta"]);

        // One centroid per population, each aggregating its 6 members, carrying a 25-dim mean.
        Assert.NotNull(scatter.Basis);
        Assert.Equal(Dims, scatter.Basis!.Means.Length);
        Assert.Equal(2, scatter.Centroids.Count);
        Assert.All(scatter.Centroids, c =>
        {
            Assert.Equal(6, c.SampleCount);
            Assert.Equal(Dims, c.Coordinates.Length);
        });
        Assert.Contains(scatter.Centroids, c => c.Label == "Alpha");
        Assert.Contains(scatter.Centroids, c => c.Label == "Beta");
    }

    [Fact]
    public async Task EraScatter_SkipsMalformedGroup_ButKeepsRestOfPopulation()
    {
        // "Bad" has two valid members plus one malformed group ("1,2,3" — not 25 values). The malformed
        // group must cost one point, not the whole population.
        var badCoordinates = Cluster(2, offset: 1) + ";1,2,3";
        var eraId = await SeedPcaEraAsync(
            ("Alpha", Cluster(10, offset: 0)),
            ("Bad", badCoordinates));

        var response = await Client.GetAsync($"/api/g25-pca/eras/{eraId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scatter = await response.Content.ReadFromJsonAsync<PcaEraScatterContract.Response>();
        Assert.NotNull(scatter);

        // 10 (Alpha) + 2 valid (Bad) = 12; the malformed sub-group is dropped, not the row.
        Assert.Equal(12, scatter!.TotalSamples);
        Assert.Equal(2, scatter.Points.Count(p => p.Label == "Bad"));
        Assert.Equal(10, scatter.Points.Count(p => p.Label == "Alpha"));
        Assert.Contains(scatter.Centroids, c => c.Label == "Bad" && c.SampleCount == 2);
    }

    [Fact]
    public async Task EraScatter_SparseEra_ReturnsNullBasis()
    {
        // Below the 10-sample fit threshold → no stable 2D basis, but the sample count still reflects the
        // expanded members.
        var eraId = await SeedPcaEraAsync(("Lonely", Cluster(5, offset: 0)));

        var response = await Client.GetAsync($"/api/g25-pca/eras/{eraId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scatter = await response.Content.ReadFromJsonAsync<PcaEraScatterContract.Response>();
        Assert.NotNull(scatter);
        Assert.Null(scatter!.Basis);
        Assert.Equal(5, scatter.TotalSamples);
        Assert.Equal(0, scatter.PlottedSamples);
        Assert.Empty(scatter.Points);
    }

    [Fact]
    public async Task EraScatter_UnknownEra_Returns404()
    {
        var response = await Client.GetAsync("/api/g25-pca/eras/999999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
