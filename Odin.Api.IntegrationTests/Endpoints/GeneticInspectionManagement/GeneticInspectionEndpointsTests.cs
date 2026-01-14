using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.GeneticInspectionManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.GeneticInspectionManagement;

public class GeneticInspectionEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetAll_WhenNoInspections_ReturnsEmptyList()
    {
        // Act
        var response = await Client.GetAsync("/api/genetic-inspections");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var inspections = await response.Content.ReadFromJsonAsync<List<GetGeneticInspectionContract.Response>>();
        Assert.NotNull(inspections);
        Assert.Empty(inspections);
    }

    [Fact]
    public async Task Create_WithValidRequest_ReturnsCreated()
    {
        // Arrange
        var (rawFileId, regionIds) = await SeedTestDataAsync();

        var request = new CreateGeneticInspectionContract.Request
        {
            FirstName = "John",
            MiddleName = "William",
            LastName = "Doe",
            RawGeneticFileId = rawFileId,
            RegionIds = regionIds
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/genetic-inspections", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CreateGeneticInspectionContract.Response>();
        Assert.NotNull(result);
        Assert.Equal("John", result.FirstName);
        Assert.Equal("William", result.MiddleName);
        Assert.Equal("Doe", result.LastName);
        Assert.Equal(rawFileId, result.RawGeneticFileId);
        Assert.Equal(regionIds.Count, result.Regions.Count);
    }

    [Fact]
    public async Task Create_WithMissingFirstName_ReturnsBadRequest()
    {
        // Arrange
        var (rawFileId, regionIds) = await SeedTestDataAsync();

        var request = new CreateGeneticInspectionContract.Request
        {
            FirstName = "",
            LastName = "Doe",
            RawGeneticFileId = rawFileId,
            RegionIds = regionIds
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/genetic-inspections", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithNoRegions_ReturnsBadRequest()
    {
        // Arrange
        var (rawFileId, _) = await SeedTestDataAsync();

        var request = new CreateGeneticInspectionContract.Request
        {
            FirstName = "John",
            LastName = "Doe",
            RawGeneticFileId = rawFileId,
            RegionIds = []
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/genetic-inspections", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetById_WhenExists_ReturnsInspection()
    {
        // Arrange
        var createdInspection = await CreateTestInspectionAsync();

        // Act
        var response = await Client.GetAsync($"/api/genetic-inspections/{createdInspection.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var inspection = await response.Content.ReadFromJsonAsync<GetGeneticInspectionContract.Response>();
        Assert.NotNull(inspection);
        Assert.Equal(createdInspection.Id, inspection.Id);
        Assert.Equal(createdInspection.FirstName, inspection.FirstName);
        Assert.Equal(createdInspection.LastName, inspection.LastName);
    }

    [Fact]
    public async Task GetById_WhenNotExists_ReturnsNotFound()
    {
        // Act
        var response = await Client.GetAsync("/api/genetic-inspections/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenExists_ReturnsNoContent()
    {
        // Arrange
        var createdInspection = await CreateTestInspectionAsync();

        // Act
        var response = await Client.DeleteAsync($"/api/genetic-inspections/{createdInspection.Id}");

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify deletion
        var getResponse = await Client.GetAsync($"/api/genetic-inspections/{createdInspection.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_WhenNotExists_ReturnsNotFound()
    {
        // Act
        var response = await Client.DeleteAsync("/api/genetic-inspections/99999");

        // Assert
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAll_AfterCreating_ReturnsInspections()
    {
        // Arrange
        await CreateTestInspectionAsync("John", "Doe");
        await CreateTestInspectionAsync("Jane", "Smith");

        // Act
        var response = await Client.GetAsync("/api/genetic-inspections");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var inspections = await response.Content.ReadFromJsonAsync<List<GetGeneticInspectionContract.Response>>();
        Assert.NotNull(inspections);
        Assert.Equal(2, inspections.Count);
        Assert.Contains(inspections, i => i.FirstName == "John" && i.LastName == "Doe");
        Assert.Contains(inspections, i => i.FirstName == "Jane" && i.LastName == "Smith");
    }

    [Fact]
    public async Task Create_IncludesRegionsWithCountryInfo()
    {
        // Arrange
        var (rawFileId, regionIds) = await SeedTestDataAsync();

        var request = new CreateGeneticInspectionContract.Request
        {
            FirstName = "Test",
            LastName = "User",
            RawGeneticFileId = rawFileId,
            RegionIds = regionIds
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/genetic-inspections", request);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CreateGeneticInspectionContract.Response>();
        Assert.NotNull(result);
        Assert.All(result.Regions, r =>
        {
            Assert.NotEmpty(r.Name);
            Assert.NotEmpty(r.CountryName);
        });
    }

    private async Task<(int RawFileId, List<int> RegionIds)> SeedTestDataAsync()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        // Seed raw genetic file
        var rawFile = new RawGeneticFile
        {
            FileName = "test_genetic_data.txt",
            RawData = "Test genetic data content"u8.ToArray()
        };
        dbContext.RawGeneticFiles.Add(rawFile);

        // Seed countries and regions
        var country = new Country { Name = "Test Country" };
        dbContext.Countries.Add(country);
        await dbContext.SaveChangesAsync();

        var region1 = new Region { Name = "Test Region 1", CountryId = country.Id, Country = country };
        var region2 = new Region { Name = "Test Region 2", CountryId = country.Id, Country = country };
        dbContext.Regions.AddRange(region1, region2);
        await dbContext.SaveChangesAsync();

        return (rawFile.Id, [region1.Id, region2.Id]);
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
