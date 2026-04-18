using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
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

    private async Task<(int eraId, int distanceFileId, int ethnicityId, int regionId, int admixtureFileId)> SeedG25ReferencesAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        var era = new G25Era
        {
            Name = $"Era-{Guid.NewGuid():N}",
            CreatedAt = now, CreatedBy = "test", UpdatedAt = now, UpdatedBy = "test"
        };
        db.G25Eras.Add(era);
        await db.SaveChangesAsync();

        var distFile = new G25DistanceFile
        {
            Title = $"Dist-{Guid.NewGuid():N}",
            Content = SourceCsv,
            G25EraId = era.Id,
            CreatedAt = now, CreatedBy = "test", UpdatedAt = now, UpdatedBy = "test"
        };
        db.G25DistanceFiles.Add(distFile);

        var continent = new G25Continent
        {
            Name = $"Cont-{Guid.NewGuid():N}",
            CreatedAt = now, CreatedBy = "test", UpdatedAt = now, UpdatedBy = "test"
        };
        db.G25Continents.Add(continent);
        await db.SaveChangesAsync();

        var ethnicity = new G25Ethnicity
        {
            Name = $"Ethn-{Guid.NewGuid():N}",
            G25ContinentId = continent.Id,
            CreatedAt = now, CreatedBy = "test", UpdatedAt = now, UpdatedBy = "test"
        };
        db.G25Ethnicities.Add(ethnicity);
        await db.SaveChangesAsync();

        var region = new G25Region
        {
            Name = $"Region-{Guid.NewGuid():N}",
            G25EthnicityId = ethnicity.Id,
            CreatedAt = now, CreatedBy = "test", UpdatedAt = now, UpdatedBy = "test"
        };
        db.G25Regions.Add(region);
        await db.SaveChangesAsync();

        var admixFile = new G25AdmixtureFile
        {
            Name = $"Admix-{Guid.NewGuid():N}",
            Content = SourceCsv,
            G25RegionId = region.Id,
            CreatedAt = now, CreatedBy = "test", UpdatedAt = now, UpdatedBy = "test"
        };
        db.G25AdmixtureFiles.Add(admixFile);
        await db.SaveChangesAsync();

        return (era.Id, distFile.Id, ethnicity.Id, region.Id, admixFile.Id);
    }

    private void SetAppRole(string role)
    {
        Client.DefaultRequestHeaders.Remove("X-Test-App-Role");
        Client.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-App-Role", role);
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
    public async Task Distances_WithSourceDistanceFileId_Returns200()
    {
        var (_, distFileId, _, _, _) = await SeedG25ReferencesAsync();
        var body = new ComputeDistancesContract.Request
        {
            TargetCoordinates = TargetCsv,
            SourceDistanceFileId = distFileId,
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
    public async Task Distances_WithUnknownFileId_Returns404()
    {
        var body = new ComputeDistancesContract.Request
        {
            TargetCoordinates = TargetCsv,
            SourceDistanceFileId = 999_999
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
        var (_, distFileId, _, _, _) = await SeedG25ReferencesAsync();
        var body = new ComputeDistancesContract.Request
        {
            TargetCoordinates = TargetCsv,
            SourceContent = SourceCsv,
            SourceDistanceFileId = distFileId
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
    public async Task AdmixtureSingle_WithSourceAdmixtureFileId_Returns200()
    {
        var (_, _, _, _, admixFileId) = await SeedG25ReferencesAsync();
        var body = new ComputeAdmixtureSingleContract.Request
        {
            TargetCoordinates = TargetCsv,
            SourceAdmixtureFileId = admixFileId,
            CyclesMultiplier = 1
        };

        var response = await Client.PostAsJsonAsync("/api/g25-calculations/admixture/single", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdmixtureSingle_WithSourceRegionIds_Returns200()
    {
        var (_, _, _, regionId, _) = await SeedG25ReferencesAsync();
        var body = new ComputeAdmixtureSingleContract.Request
        {
            TargetCoordinates = TargetCsv,
            SourceRegionIds = [regionId],
            CyclesMultiplier = 1
        };

        var response = await Client.PostAsJsonAsync("/api/g25-calculations/admixture/single", body);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdmixtureSingle_WithRegionMissingAdmixtureFile_Returns404()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var continent = new G25Continent
        {
            Name = $"Cont-{Guid.NewGuid():N}",
            CreatedAt = now, CreatedBy = "t", UpdatedAt = now, UpdatedBy = "t"
        };
        db.G25Continents.Add(continent);
        await db.SaveChangesAsync();
        var ethnicity = new G25Ethnicity
        {
            Name = $"Ethn-{Guid.NewGuid():N}",
            G25ContinentId = continent.Id,
            CreatedAt = now, CreatedBy = "t", UpdatedAt = now, UpdatedBy = "t"
        };
        db.G25Ethnicities.Add(ethnicity);
        await db.SaveChangesAsync();
        var region = new G25Region
        {
            Name = $"Region-{Guid.NewGuid():N}",
            G25EthnicityId = ethnicity.Id,
            CreatedAt = now, CreatedBy = "t", UpdatedAt = now, UpdatedBy = "t"
        };
        db.G25Regions.Add(region);
        await db.SaveChangesAsync();

        var body = new ComputeAdmixtureSingleContract.Request
        {
            TargetCoordinates = TargetCsv,
            SourceRegionIds = [region.Id],
            CyclesMultiplier = 1
        };

        var response = await Client.PostAsJsonAsync("/api/g25-calculations/admixture/single", body);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdmixtureSingle_WithAllThreeSources_Returns400()
    {
        var (_, _, _, regionId, admixFileId) = await SeedG25ReferencesAsync();
        var body = new ComputeAdmixtureSingleContract.Request
        {
            TargetCoordinates = TargetCsv,
            SourceContent = SourceCsv,
            SourceAdmixtureFileId = admixFileId,
            SourceRegionIds = [regionId]
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

    // ── Access-control hardening ────────────────────────────────────

    [Fact]
    public async Task GetDistanceFileById_AsEmailVerifiedUser_ReturnsForbidden()
    {
        var (_, distFileId, _, _, _) = await SeedG25ReferencesAsync();
        SetAppRole("User");

        var response = await Client.GetAsync($"/api/g25-distance-files/{distFileId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetDistanceFileById_AsAdmin_Returns200()
    {
        var (_, distFileId, _, _, _) = await SeedG25ReferencesAsync();
        SetAppRole("Admin");

        var response = await Client.GetAsync($"/api/g25-distance-files/{distFileId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetAdmixtureFileById_AsEmailVerifiedUser_ReturnsForbidden()
    {
        var (_, _, _, _, admixFileId) = await SeedG25ReferencesAsync();
        SetAppRole("User");

        var response = await Client.GetAsync($"/api/g25-admixture-files/{admixFileId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAdmixtureFileByRegionId_AsEmailVerifiedUser_ReturnsForbidden()
    {
        var (_, _, _, regionId, _) = await SeedG25ReferencesAsync();
        SetAppRole("User");

        var response = await Client.GetAsync($"/api/g25-admixture-files/by-region/{regionId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetAdmixtureFileByRegionId_AsAdmin_Returns200()
    {
        var (_, _, _, regionId, _) = await SeedG25ReferencesAsync();
        SetAppRole("Admin");

        var response = await Client.GetAsync($"/api/g25-admixture-files/by-region/{regionId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
