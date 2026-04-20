using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25Calculations.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.G25Calculations;

public class G25CalculationEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string SourceCsv =
        "PopA,0.01,0.02,0.03\n" +
        "PopB,0.1,0.2,0.3\n" +
        "PopC,0.5,0.4,0.3\n" +
        "PopD,1.0,1.0,1.0";

    private const string TargetCsv = "TargetX,0.02,0.03,0.04";

    private const string MultiTargetCsv =
        "TargetX,0.02,0.03,0.04\n" +
        "TargetY,0.5,0.5,0.5";

    private static readonly (string Label, string Coords)[] DistanceSamples =
    [
        ("PopA", "0.01,0.02,0.03"),
        ("PopB", "0.1,0.2,0.3"),
        ("PopC", "0.5,0.4,0.3"),
        ("PopD", "1.0,1.0,1.0"),
    ];

    private async Task<int> SeedG25DistanceEraWithSamplesAsync()
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

        foreach (var sample in DistanceSamples)
        {
            db.G25DistancePopulationSamples.Add(new G25DistancePopulationSample
            {
                Label = sample.Label,
                Coordinates = sample.Coords,
                Ids = string.Empty,
                G25DistanceEraId = era.Id,
                CreatedAt = now, CreatedBy = "test", UpdatedAt = now, UpdatedBy = "test"
            });
        }
        await db.SaveChangesAsync();

        return era.Id;
    }

    // ── POST /api/g25-calculations/distances ────────────────────────

    [Fact]
    public async Task Distances_WithSourceContent_Returns200AndResults()
    {
        var body = new ComputeDistancesContract.Request
        {
            TargetCoordinates = TargetCsv,
            SourceContent = SourceCsv,
            MaxResults = 2
        };

        var response = await Client.PostAsJsonAsync("/api/g25-calculations/distances", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ComputeDistancesContract.Response>();
        Assert.NotNull(result);
        Assert.Single(result!.Results);
        Assert.Equal("TargetX", result.Results[0].TargetName);
        Assert.Equal(2, result.Results[0].Rows.Count);
        Assert.Equal("PopA", result.Results[0].Rows[0].Name);
    }

    [Fact]
    public async Task Distances_WithG25DistanceEraId_Returns200()
    {
        var eraId = await SeedG25DistanceEraWithSamplesAsync();
        var body = new ComputeDistancesContract.Request
        {
            TargetCoordinates = TargetCsv,
            G25DistanceEraId = eraId,
            MaxResults = 10
        };

        var response = await Client.PostAsJsonAsync("/api/g25-calculations/distances", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ComputeDistancesContract.Response>();
        Assert.NotNull(result);
        Assert.Single(result!.Results);
        Assert.True(result.Results[0].Rows.Count > 0);
    }

    [Fact]
    public async Task Distances_WithUnknownEraId_Returns404()
    {
        var body = new ComputeDistancesContract.Request
        {
            TargetCoordinates = TargetCsv,
            G25DistanceEraId = 999_999
        };

        var response = await Client.PostAsJsonAsync("/api/g25-calculations/distances", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Distances_WithNoSource_Returns400()
    {
        var body = new ComputeDistancesContract.Request
        {
            TargetCoordinates = TargetCsv
        };

        var response = await Client.PostAsJsonAsync("/api/g25-calculations/distances", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Distances_WithBothSources_Returns400()
    {
        var eraId = await SeedG25DistanceEraWithSamplesAsync();
        var body = new ComputeDistancesContract.Request
        {
            TargetCoordinates = TargetCsv,
            SourceContent = SourceCsv,
            G25DistanceEraId = eraId
        };

        var response = await Client.PostAsJsonAsync("/api/g25-calculations/distances", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Distances_WithEmptyTarget_Returns400()
    {
        var body = new ComputeDistancesContract.Request
        {
            TargetCoordinates = "",
            SourceContent = SourceCsv
        };

        var response = await Client.PostAsJsonAsync("/api/g25-calculations/distances", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Distances_AsUser_ReturnsForbidden_WhenEmailNotVerified()
    {
        Client.DefaultRequestHeaders.Remove("X-Test-Email-Verified");
        Client.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-Email-Verified", "false");

        var body = new ComputeDistancesContract.Request
        {
            TargetCoordinates = TargetCsv,
            SourceContent = SourceCsv
        };

        var response = await Client.PostAsJsonAsync("/api/g25-calculations/distances", body);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── POST /api/g25-calculations/admixture/single ─────────────────

    [Fact]
    public async Task AdmixtureSingle_WithSourceContent_Returns200AndPercentagesSumToHundred()
    {
        var body = new ComputeAdmixtureSingleContract.Request
        {
            TargetCoordinates = TargetCsv,
            SourceContent = SourceCsv,
            CyclesMultiplier = 1,
            Aggregate = false,
            PrintZeroes = true
        };

        var response = await Client.PostAsJsonAsync("/api/g25-calculations/admixture/single", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ComputeAdmixtureSingleContract.Response>();
        Assert.NotNull(result);
        Assert.Single(result!.Results);
        var single = result.Results[0];
        Assert.Equal("TargetX", single.TargetName);
        var sum = single.Rows.Sum(r => r.Pct);
        Assert.InRange(sum, 99.999, 100.001);
    }

    [Fact]
    public async Task AdmixtureSingle_WithoutSourceContent_Returns400()
    {
        var body = new ComputeAdmixtureSingleContract.Request
        {
            TargetCoordinates = TargetCsv,
            SourceContent = "",
            CyclesMultiplier = 1
        };

        var response = await Client.PostAsJsonAsync("/api/g25-calculations/admixture/single", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── POST /api/g25-calculations/admixture/multi ──────────────────

    [Fact]
    public async Task AdmixtureMulti_WithSourceContent_Returns200WithBothTargets()
    {
        var body = new ComputeAdmixtureMultiContract.Request
        {
            TargetCoordinates = MultiTargetCsv,
            SourceContent = SourceCsv,
            CyclesMultiplier = 1,
            FastMode = true,
            Aggregate = false,
            PrintZeroes = true
        };

        var response = await Client.PostAsJsonAsync("/api/g25-calculations/admixture/multi", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<ComputeAdmixtureMultiContract.Response>();
        Assert.NotNull(result);
        Assert.Equal(2, result!.Targets.Count);
        Assert.Equal(result.Targets.Count, result.Targets.Select(t => t.Name).Distinct().Count());
    }

    [Fact]
    public async Task AdmixtureMulti_WithoutSourceContent_Returns400()
    {
        var body = new ComputeAdmixtureMultiContract.Request
        {
            TargetCoordinates = MultiTargetCsv,
            SourceContent = ""
        };

        var response = await Client.PostAsJsonAsync("/api/g25-calculations/admixture/multi", body);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
