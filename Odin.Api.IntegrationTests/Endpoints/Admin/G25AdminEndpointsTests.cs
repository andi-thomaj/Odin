using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.Admin.Models;
using Odin.Api.Endpoints.OrderManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;
using static Odin.Api.IntegrationTests.Fakers.TestDataHelper;

namespace Odin.Api.IntegrationTests.Endpoints.Admin;

public class G25AdminEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static readonly (string Label, string Coords)[] SamplesA =
    [
        ("PopA1", "0.01,0.02,0.03"),
        ("PopA2", "0.10,0.20,0.30"),
        ("PopA3", "0.50,0.40,0.30"),
        ("PopA4", "1.00,1.00,1.00"),
    ];

    private static readonly (string Label, string Coords)[] SamplesB =
    [
        ("PopB1", "0.20,0.10,0.05"),
        ("PopB2", "0.60,0.40,0.20"),
    ];

    private const string TargetCoordinates = "TestTarget,0.02,0.03,0.04";

    [Fact]
    public async Task RecomputeDistanceResults_NoEras_ReturnsZeroErasAndNoUpserts()
    {
        var response = await Client.PostAsJsonAsync(
            "/api/admin/g25/recompute-distance-results",
            new RecomputeG25DistancesContract.Request());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RecomputeG25DistancesContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(0, body!.ErasConsidered);
        Assert.Equal(0, body.ResultsUpserted);
        Assert.Equal(0, body.InspectionsProcessed);
    }

    [Fact]
    public async Task RecomputeDistanceResults_InspectionWithNoPriorResults_InsertsPerEra()
    {
        await SeedEraAsync("EraAlpha", SamplesA);
        await SeedEraAsync("EraBeta", SamplesB);

        var orderId = await CreateG25OrderAsync();
        var inspectionId = await GetInspectionIdForOrderAsync(orderId);

        await DeleteAllDistanceResultsAsync(inspectionId);

        var response = await Client.PostAsJsonAsync(
            "/api/admin/g25/recompute-distance-results",
            new RecomputeG25DistancesContract.Request());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RecomputeG25DistancesContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(2, body!.ErasConsidered);
        Assert.Equal(1, body.InspectionsProcessed);
        Assert.Equal(2, body.ResultsUpserted);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var results = await db.G25DistanceResults.AsNoTracking()
            .Where(r => r.GeneticInspectionId == inspectionId)
            .ToListAsync();
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.NotEmpty(r.Populations));
        Assert.All(results, r => Assert.StartsWith("v", r.ResultsVersion));
    }

    [Fact]
    public async Task RecomputeDistanceResults_InspectionWithExistingResults_UpdatesVersionAndPopulations()
    {
        var eraId = await SeedEraAsync("EraAlpha", SamplesA);

        var orderId = await CreateG25OrderAsync();
        var inspectionId = await GetInspectionIdForOrderAsync(orderId);

        (int OriginalId, string OriginalVersion, DateTime OriginalUpdatedAt) snapshot;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var first = await db.G25DistanceResults.AsNoTracking()
                .Where(r => r.GeneticInspectionId == inspectionId && r.G25DistanceEraId == eraId)
                .FirstAsync();
            snapshot = (first.Id, first.ResultsVersion, first.UpdatedAt);
        }

        await Task.Delay(1100);

        var response = await Client.PostAsJsonAsync(
            "/api/admin/g25/recompute-distance-results",
            new RecomputeG25DistancesContract.Request { InspectionIds = [inspectionId] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RecomputeG25DistancesContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(1, body!.ErasConsidered);
        Assert.Equal(1, body.InspectionsProcessed);
        Assert.Equal(1, body.ResultsUpserted);

        await using var verifyScope = Factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var updated = await verifyDb.G25DistanceResults.AsNoTracking()
            .Where(r => r.GeneticInspectionId == inspectionId && r.G25DistanceEraId == eraId)
            .FirstAsync();

        Assert.Equal(snapshot.OriginalId, updated.Id);
        Assert.NotEqual(snapshot.OriginalVersion, updated.ResultsVersion);
        Assert.True(updated.UpdatedAt > snapshot.OriginalUpdatedAt);
        Assert.NotEmpty(updated.Populations);

        var duplicateCount = await verifyDb.G25DistanceResults.AsNoTracking()
            .CountAsync(r => r.GeneticInspectionId == inspectionId && r.G25DistanceEraId == eraId);
        Assert.Equal(1, duplicateCount);
    }

    [Fact]
    public async Task RecomputeDistanceResults_MultipleErasWithSamples_ProducesResultPerEra()
    {
        await SeedEraAsync("EraAlpha", SamplesA);
        await SeedEraAsync("EraBeta", SamplesB);

        var orderId = await CreateG25OrderAsync();
        var inspectionId = await GetInspectionIdForOrderAsync(orderId);

        var response = await Client.PostAsJsonAsync(
            "/api/admin/g25/recompute-distance-results",
            new RecomputeG25DistancesContract.Request { InspectionIds = [inspectionId] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RecomputeG25DistancesContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(2, body!.ErasConsidered);
        Assert.Equal(1, body.InspectionsProcessed);
        Assert.Equal(2, body.ResultsUpserted);

        var getResult = await Client.GetAsync($"/api/orders/{orderId}/g25-result");
        Assert.Equal(HttpStatusCode.OK, getResult.StatusCode);
        var resultBody = await getResult.Content.ReadFromJsonAsync<GetOrderG25ResultContract.Response>(JsonOptions);
        Assert.NotNull(resultBody);
        Assert.Equal(2, resultBody!.DistanceEras.Count);
        Assert.All(resultBody.DistanceEras, e => Assert.NotEmpty(e.Populations));
    }

    [Fact]
    public async Task RecomputeDistanceResults_SamplesWithLabelPrefixInCoordinates_UsesSampleLabelNotEmbedded()
    {
        (string Label, string Coords)[] prefixed =
        [
            ("PopA1", "EmbeddedLabel1*(foo),0.01,0.02,0.03"),
            ("PopA2", "EmbeddedLabel2*(bar),0.10,0.20,0.30"),
            ("PopA3", "EmbeddedLabel3*(baz),0.50,0.40,0.30"),
        ];
        await SeedEraAsync("EraPrefixed", prefixed);

        var orderId = await CreateG25OrderAsync();
        var inspectionId = await GetInspectionIdForOrderAsync(orderId);
        await DeleteAllDistanceResultsAsync(inspectionId);

        var response = await Client.PostAsJsonAsync(
            "/api/admin/g25/recompute-distance-results",
            new RecomputeG25DistancesContract.Request { InspectionIds = [inspectionId] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RecomputeG25DistancesContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(1, body!.ErasConsidered);
        Assert.Equal(1, body.InspectionsProcessed);
        Assert.Equal(1, body.ResultsUpserted);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var result = await db.G25DistanceResults.AsNoTracking()
            .FirstAsync(r => r.GeneticInspectionId == inspectionId);
        Assert.NotEmpty(result.Populations);
        var names = result.Populations.Select(p => p.Name).ToList();
        Assert.Contains("PopA1", names);
        Assert.Contains("PopA2", names);
        Assert.Contains("PopA3", names);
        Assert.DoesNotContain(names, n => n.Contains("EmbeddedLabel", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecomputeDistanceResults_SamplesWithLeadingCommaCoordinates_StillUpserts()
    {
        (string Label, string Coords)[] leadingComma =
        [
            ("PopA1", ",0.01,0.02,0.03"),
            ("PopA2", ",0.10,0.20,0.30"),
            ("PopA3", ",0.50,0.40,0.30"),
        ];
        await SeedEraAsync("EraLeadingComma", leadingComma);

        var orderId = await CreateG25OrderAsync();
        var inspectionId = await GetInspectionIdForOrderAsync(orderId);
        await DeleteAllDistanceResultsAsync(inspectionId);

        var response = await Client.PostAsJsonAsync(
            "/api/admin/g25/recompute-distance-results",
            new RecomputeG25DistancesContract.Request { InspectionIds = [inspectionId] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RecomputeG25DistancesContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(1, body!.ErasConsidered);
        Assert.Equal(1, body.InspectionsProcessed);
        Assert.Equal(1, body.ResultsUpserted);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var result = await db.G25DistanceResults.AsNoTracking()
            .FirstAsync(r => r.GeneticInspectionId == inspectionId);
        Assert.NotEmpty(result.Populations);
        Assert.Contains(result.Populations, p => p.Name == "PopA1" || p.Name == "PopA2" || p.Name == "PopA3");
    }

    [Fact]
    public async Task RecomputeDistanceResults_SamplesWithMultilineCoordinates_StillUpserts()
    {
        (string Label, string Coords)[] multiline =
        [
            ("PopA1", "EmbeddedLabel1,0.01,0.02,0.03\n---GroupHeader---"),
            ("PopA2", "EmbeddedLabel2,0.10,0.20,0.30"),
            ("PopA3", "EmbeddedLabel3,0.50,0.40,0.30"),
        ];
        await SeedEraAsync("EraMultiline", multiline);

        var orderId = await CreateG25OrderAsync();
        var inspectionId = await GetInspectionIdForOrderAsync(orderId);
        await DeleteAllDistanceResultsAsync(inspectionId);

        var response = await Client.PostAsJsonAsync(
            "/api/admin/g25/recompute-distance-results",
            new RecomputeG25DistancesContract.Request { InspectionIds = [inspectionId] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RecomputeG25DistancesContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(1, body!.ErasConsidered);
        Assert.Equal(1, body.InspectionsProcessed);
        Assert.Equal(1, body.ResultsUpserted);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var result = await db.G25DistanceResults.AsNoTracking()
            .FirstAsync(r => r.GeneticInspectionId == inspectionId);
        Assert.NotEmpty(result.Populations);
        var names = result.Populations.Select(p => p.Name).ToList();
        Assert.Contains("PopA1", names);
        Assert.Contains("PopA2", names);
        Assert.Contains("PopA3", names);
        Assert.DoesNotContain(names, n => n.Contains("EmbeddedLabel", StringComparison.Ordinal));
    }

    [Fact]
    public async Task RecomputeDistanceResults_LabelsWithSpaces_ArePreservedInResults()
    {
        (string Label, string Coords)[] withSpaces =
        [
            ("Thracian Triballi 650 BC", "0.01,0.02,0.03"),
            ("Ancient Macedonian", "0.10,0.20,0.30"),
            ("Early Medieval Slavic", "0.50,0.40,0.30"),
        ];
        await SeedEraAsync("EraSpaces", withSpaces);

        var orderId = await CreateG25OrderAsync();
        var inspectionId = await GetInspectionIdForOrderAsync(orderId);
        await DeleteAllDistanceResultsAsync(inspectionId);

        var response = await Client.PostAsJsonAsync(
            "/api/admin/g25/recompute-distance-results",
            new RecomputeG25DistancesContract.Request { InspectionIds = [inspectionId] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RecomputeG25DistancesContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(1, body!.ResultsUpserted);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var result = await db.G25DistanceResults.AsNoTracking()
            .FirstAsync(r => r.GeneticInspectionId == inspectionId);
        var names = result.Populations.Select(p => p.Name).ToList();
        Assert.Contains("Thracian Triballi 650 BC", names);
        Assert.Contains("Ancient Macedonian", names);
        Assert.Contains("Early Medieval Slavic", names);
        Assert.DoesNotContain(names, n => n.Contains('>'));
    }

    [Fact]
    public async Task RecomputeDistanceResults_EraWithoutSamples_IsSkipped()
    {
        await SeedEraAsync("EraWithSamples", SamplesA);
        await SeedEmptyEraAsync("EraNoSamples");

        var orderId = await CreateG25OrderAsync();
        var inspectionId = await GetInspectionIdForOrderAsync(orderId);

        var response = await Client.PostAsJsonAsync(
            "/api/admin/g25/recompute-distance-results",
            new RecomputeG25DistancesContract.Request { InspectionIds = [inspectionId] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RecomputeG25DistancesContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(1, body!.ErasConsidered);
        Assert.Equal(1, body.ResultsUpserted);
    }

    private async Task<int> SeedEraAsync(string eraNamePrefix, (string Label, string Coords)[] samples)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        var era = new G25DistanceEra
        {
            Name = $"{eraNamePrefix}-{Guid.NewGuid():N}",
            CreatedAt = now,
            CreatedBy = "test",
            UpdatedAt = now,
            UpdatedBy = "test"
        };
        db.G25DistanceEras.Add(era);
        await db.SaveChangesAsync();

        foreach (var sample in samples)
        {
            db.G25DistancePopulationSamples.Add(new G25DistancePopulationSample
            {
                Label = sample.Label,
                Coordinates = sample.Coords,
                Ids = string.Empty,
                G25DistanceEraId = era.Id,
                CreatedAt = now,
                CreatedBy = "test",
                UpdatedAt = now,
                UpdatedBy = "test"
            });
        }
        await db.SaveChangesAsync();
        return era.Id;
    }

    private async Task<int> SeedEmptyEraAsync(string eraNamePrefix)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        var era = new G25DistanceEra
        {
            Name = $"{eraNamePrefix}-{Guid.NewGuid():N}",
            CreatedAt = now,
            CreatedBy = "test",
            UpdatedAt = now,
            UpdatedBy = "test"
        };
        db.G25DistanceEras.Add(era);
        await db.SaveChangesAsync();
        return era.Id;
    }

    private async Task<int> CreateG25OrderAsync()
    {
        using var content = new MultipartFormDataContent
        {
            { new StringContent("G25"), "FirstName" },
            { new StringContent("Tester"), "LastName" },
            { new StringContent("Male"), "Gender" },
            { new StringContent(((int)ServiceType.g25).ToString()), "Service" },
            { new StringContent(TargetCoordinates), "G25Coordinates" }
        };
        var response = await Client.PostAsync("/api/orders", content);
        response.EnsureSuccessStatusCode();
        var body = (await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions))!;
        return body.Id;
    }

    private async Task<int> GetInspectionIdForOrderAsync(int orderId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var inspection = await db.G25GeneticInspections.AsNoTracking()
            .FirstAsync(gi => gi.OrderId == orderId);
        return inspection.Id;
    }

    private async Task DeleteAllDistanceResultsAsync(int inspectionId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var existing = await db.G25DistanceResults
            .Where(r => r.GeneticInspectionId == inspectionId)
            .ToListAsync();
        db.G25DistanceResults.RemoveRange(existing);
        await db.SaveChangesAsync();
    }
}
