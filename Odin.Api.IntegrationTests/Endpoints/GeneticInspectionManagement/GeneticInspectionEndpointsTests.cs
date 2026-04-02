using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.GeneticInspectionManagement.Models;
using Odin.Api.IntegrationTests.Fakers;
using Odin.Api.IntegrationTests.Infrastructure;
using static Odin.Api.IntegrationTests.Fakers.TestDataHelper;

namespace Odin.Api.IntegrationTests.Endpoints.GeneticInspectionManagement;

public class GeneticInspectionEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // ── GET /api/genetic-inspections ───────────────────────────────

    [Fact]
    public async Task GetAll_WhenNoInspections_ReturnsEmptyList()
    {
        var response = await Client.GetAsync("/api/genetic-inspections");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var inspections = await response.Content.ReadFromJsonAsync<List<GetGeneticInspectionContract.Response>>();
        Assert.NotNull(inspections);
        Assert.Empty(inspections!);
    }

    [Fact]
    public async Task GetAll_AfterCreating_ReturnsInspections()
    {
        await CreateTestInspectionAsync("John", "Doe");
        await CreateTestInspectionAsync("Jane", "Smith");

        var response = await Client.GetAsync("/api/genetic-inspections");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var inspections = await response.Content.ReadFromJsonAsync<List<GetGeneticInspectionContract.Response>>();
        Assert.NotNull(inspections);
        Assert.Equal(2, inspections!.Count);
        Assert.Contains(inspections, i => i.FirstName == "John" && i.LastName == "Doe");
        Assert.Contains(inspections, i => i.FirstName == "Jane" && i.LastName == "Smith");
    }

    [Fact]
    public async Task GetAll_AsUser_ReturnsForbidden()
    {
        using var userClient = CreateClientWithRole(Factory, "auth0|integration-default", "User");

        var response = await userClient.GetAsync("/api/genetic-inspections");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── POST /api/genetic-inspections (create) ─────────────────────

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreated()
    {
        var (rawFileId, regionIds) = await SeedTestDataAsync();

        var request = new CreateGeneticInspectionContract.Request
        {
            FirstName = "John",
            MiddleName = "William",
            LastName = "Doe",
            RawGeneticFileId = rawFileId,
            RegionIds = regionIds
        };

        var response = await Client.PostAsJsonAsync("/api/genetic-inspections", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateGeneticInspectionContract.Response>();
        Assert.NotNull(result);
        Assert.Equal("John", result!.FirstName);
        Assert.Equal("William", result.MiddleName);
        Assert.Equal("Doe", result.LastName);
        Assert.Equal(rawFileId, result.RawGeneticFileId);
        Assert.Equal(regionIds.Count, result.Regions.Count);
    }

    [Fact]
    public async Task Create_WithMissingFirstName_ReturnsBadRequest()
    {
        var (rawFileId, regionIds) = await SeedTestDataAsync();

        var request = new CreateGeneticInspectionContract.Request
        {
            FirstName = "",
            LastName = "Doe",
            RawGeneticFileId = rawFileId,
            RegionIds = regionIds
        };

        var response = await Client.PostAsJsonAsync("/api/genetic-inspections", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithNoRegions_ReturnsBadRequest()
    {
        var (rawFileId, _) = await SeedTestDataAsync();

        var request = new CreateGeneticInspectionContract.Request
        {
            FirstName = "John",
            LastName = "Doe",
            RawGeneticFileId = rawFileId,
            RegionIds = []
        };

        var response = await Client.PostAsJsonAsync("/api/genetic-inspections", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_IncludesRegionsWithEthnicityInfo()
    {
        var (rawFileId, regionIds) = await SeedTestDataAsync();

        var request = new CreateGeneticInspectionContract.Request
        {
            FirstName = "Test",
            LastName = "User",
            RawGeneticFileId = rawFileId,
            RegionIds = regionIds
        };

        var response = await Client.PostAsJsonAsync("/api/genetic-inspections", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateGeneticInspectionContract.Response>();
        Assert.NotNull(result);
        Assert.All(result!.Regions, r =>
        {
            Assert.NotEmpty(r.Name);
            Assert.NotEmpty(r.EthnicityName);
        });
    }

    [Fact]
    public async Task Create_AsUserRole_ReturnsForbidden()
    {
        using var userClient = CreateClientWithRole(Factory, "auth0|integration-default", "User");
        var (rawFileId, regionIds) = await SeedTestDataAsync();

        var request = new CreateGeneticInspectionContract.Request
        {
            FirstName = "Test",
            LastName = "User",
            RawGeneticFileId = rawFileId,
            RegionIds = regionIds
        };

        var response = await userClient.PostAsJsonAsync("/api/genetic-inspections", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── GET /api/genetic-inspections/{id} ──────────────────────────

    [Fact]
    public async Task GetById_WhenExists_ReturnsInspection()
    {
        var createdInspection = await CreateTestInspectionAsync();

        var response = await Client.GetAsync($"/api/genetic-inspections/{createdInspection.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var inspection = await response.Content.ReadFromJsonAsync<GetGeneticInspectionContract.Response>();
        Assert.NotNull(inspection);
        Assert.Equal(createdInspection.Id, inspection!.Id);
    }

    [Fact]
    public async Task GetById_WhenNotExists_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/genetic-inspections/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── DELETE /api/genetic-inspections/{id} ────────────────────────

    [Fact]
    public async Task Delete_WhenExists_ReturnsNoContent()
    {
        var createdInspection = await CreateTestInspectionAsync();

        var response = await Client.DeleteAsync($"/api/genetic-inspections/{createdInspection.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await Client.GetAsync($"/api/genetic-inspections/{createdInspection.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenNotExists_ReturnsNotFound()
    {
        var response = await Client.DeleteAsync("/api/genetic-inspections/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_AsNonAdmin_ReturnsForbidden()
    {
        var createdInspection = await CreateTestInspectionAsync();

        using var userClient = CreateClientWithRole(Factory, "auth0|integration-default", "User");
        var response = await userClient.DeleteAsync($"/api/genetic-inspections/{createdInspection.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── POST /api/genetic-inspections/{id}/genetic-file ────────────

    [Fact]
    public async Task UploadGeneticFile_InvalidExtension_ReturnsBadRequest()
    {
        var createdInspection = await CreateTestInspectionAsync();
        var fileBytes = "bad data"u8.ToArray();

        using var content = new MultipartFormDataContent();
        var filePart = new ByteArrayContent(fileBytes);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        content.Add(filePart, "file", "bad.exe");

        var response = await Client.PostAsync(
            $"/api/genetic-inspections/{createdInspection.Id}/genetic-file", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UploadGeneticFile_NonExistentInspection_ReturnsNotFound()
    {
        var fileBytes = "data"u8.ToArray();
        using var content = new MultipartFormDataContent();
        var filePart = new ByteArrayContent(fileBytes);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        content.Add(filePart, "file", "test.txt");

        var response = await Client.PostAsync("/api/genetic-inspections/99999/genetic-file", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GET /api/genetic-inspections/{id}/genetic-file/download ────

    [Fact]
    public async Task DownloadGeneticFile_WhenExists_ReturnsFile()
    {
        var createdInspection = await CreateTestInspectionAsync();

        var response = await Client.GetAsync(
            $"/api/genetic-inspections/{createdInspection.Id}/genetic-file/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.NotEmpty(bytes);
    }

    [Fact]
    public async Task DownloadGeneticFile_NonExistent_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/genetic-inspections/99999/genetic-file/download");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── DELETE /api/genetic-inspections/{id}/genetic-file ───────────

    [Fact]
    public async Task DeleteGeneticFile_WhenExists_ReturnsNoContent()
    {
        var createdInspection = await CreateTestInspectionAsync();

        var response = await Client.DeleteAsync(
            $"/api/genetic-inspections/{createdInspection.Id}/genetic-file");

        Assert.True(response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.OK);
    }

    // ── POST /api/genetic-inspections/{id}/qpadm-result ────────────

    [Fact]
    public async Task SubmitQpadmResult_WithValidData_ReturnsCreated()
    {
        await using var seedScope = Factory.Services.CreateAsyncScope();
        var seeder = seedScope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedReferenceCatalogAsync();

        var createdInspection = await CreateTestInspectionAsync();

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var era = await db.Eras.Include(e => e.Populations).FirstAsync();
        var pops = era.Populations.Take(2).ToList();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(era.Id.ToString()), "EraGroups[0].EraId");
        form.Add(new StringContent("0.05"), "EraGroups[0].PiValue");
        form.Add(new StringContent("WHG, EHG"), "EraGroups[0].RightSources");
        form.Add(new StringContent("Anatolia_N"), "EraGroups[0].LeftSources");
        form.Add(new StringContent(pops[0].Id.ToString()), "EraGroups[0].Populations[0].PopulationId");
        form.Add(new StringContent("60"), "EraGroups[0].Populations[0].Percentage");
        form.Add(new StringContent("1.2"), "EraGroups[0].Populations[0].StandardError");
        form.Add(new StringContent("2.5"), "EraGroups[0].Populations[0].ZScore");
        form.Add(new StringContent(pops[1].Id.ToString()), "EraGroups[0].Populations[1].PopulationId");
        form.Add(new StringContent("40"), "EraGroups[0].Populations[1].Percentage");
        form.Add(new StringContent("0.8"), "EraGroups[0].Populations[1].StandardError");
        form.Add(new StringContent("1.9"), "EraGroups[0].Populations[1].ZScore");
        form.Add(new StringContent("R1b-M343"), "PaternalHaplogroup");

        var response = await Client.PostAsync(
            $"/api/genetic-inspections/{createdInspection.Id}/qpadm-result", form);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<SubmitQpadmResultContract.Response>(JsonOptions);
        Assert.NotNull(result);
        Assert.NotEmpty(result!.EraGroups);
    }

    [Fact]
    public async Task SubmitQpadmResult_PercentagesNot100_ReturnsBadRequest()
    {
        await using var seedScope = Factory.Services.CreateAsyncScope();
        var seeder = seedScope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedReferenceCatalogAsync();

        var createdInspection = await CreateTestInspectionAsync();

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var era = await db.Eras.Include(e => e.Populations).FirstAsync();
        var pop = era.Populations.First();

        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(era.Id.ToString()), "EraGroups[0].EraId");
        form.Add(new StringContent("0.05"), "EraGroups[0].PiValue");
        form.Add(new StringContent(pop.Id.ToString()), "EraGroups[0].Populations[0].PopulationId");
        form.Add(new StringContent("50"), "EraGroups[0].Populations[0].Percentage");
        form.Add(new StringContent("1.0"), "EraGroups[0].Populations[0].StandardError");
        form.Add(new StringContent("1.0"), "EraGroups[0].Populations[0].ZScore");

        var response = await Client.PostAsync(
            $"/api/genetic-inspections/{createdInspection.Id}/qpadm-result", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task SubmitQpadmResult_NoEraGroups_ReturnsBadRequest()
    {
        var createdInspection = await CreateTestInspectionAsync();

        using var form = new MultipartFormDataContent();
        // Empty form - no EraGroups

        var response = await Client.PostAsync(
            $"/api/genetic-inspections/{createdInspection.Id}/qpadm-result", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── GET /api/genetic-inspections/{id}/qpadm-result ─────────────

    [Fact]
    public async Task GetQpadmResult_AfterDirectDbSeed_ReturnsResult()
    {
        await using var seedScope = Factory.Services.CreateAsyncScope();
        var seeder = seedScope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedReferenceCatalogAsync();

        var createdInspection = await CreateTestInspectionAsync();

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var era = await db.Eras.Include(e => e.Populations).FirstAsync();
        var pops = era.Populations.Take(2).ToList();

        var qpadmResult = new QpadmResult
        {
            GeneticInspectionId = createdInspection.Id,
            CreatedBy = "test-seed"
        };
        db.QpadmResults.Add(qpadmResult);
        await db.SaveChangesAsync();

        var eraGroup = new QpadmResultEraGroup
        {
            QpadmResultId = qpadmResult.Id,
            EraId = era.Id,
            PiValue = 0.05m,
            RightSources = "WHG, EHG",
            LeftSources = "Anatolia_N"
        };
        db.Set<QpadmResultEraGroup>().Add(eraGroup);
        await db.SaveChangesAsync();

        db.Set<QpadmResultPopulation>().Add(new QpadmResultPopulation
        {
            QpadmResultEraGroupId = eraGroup.Id,
            PopulationId = pops[0].Id,
            Percentage = 60m,
            StandardError = 1.2m,
            ZScore = 2.5m
        });
        db.Set<QpadmResultPopulation>().Add(new QpadmResultPopulation
        {
            QpadmResultEraGroupId = eraGroup.Id,
            PopulationId = pops[1].Id,
            Percentage = 40m,
            StandardError = 0.8m,
            ZScore = 1.9m
        });
        await db.SaveChangesAsync();

        var response = await Client.GetAsync(
            $"/api/genetic-inspections/{createdInspection.Id}/qpadm-result");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetQpadmResult_NoResult_ReturnsNotFound()
    {
        var createdInspection = await CreateTestInspectionAsync();

        var response = await Client.GetAsync(
            $"/api/genetic-inspections/{createdInspection.Id}/qpadm-result");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private async Task<(int RawFileId, List<int> RegionIds)> SeedTestDataAsync()
    {
        var rawFileId = await SeedRawGeneticFileAsync(Factory.Services, "test_genetic_data.txt");
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services);
        return (rawFileId, regionIds);
    }

    private async Task<CreateGeneticInspectionContract.Response> CreateTestInspectionAsync(
        string firstName = "Test",
        string lastName = "User")
    {
        var (rawFileId, regionIds) = await SeedTestDataAsync();

        var request = new CreateGeneticInspectionContract.Request
        {
            FirstName = firstName,
            LastName = lastName,
            RawGeneticFileId = rawFileId,
            RegionIds = regionIds
        };

        var response = await Client.PostAsJsonAsync("/api/genetic-inspections", request);
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<CreateGeneticInspectionContract.Response>())!;
    }
}
