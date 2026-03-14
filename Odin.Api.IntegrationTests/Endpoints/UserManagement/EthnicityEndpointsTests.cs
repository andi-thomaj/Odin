using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.UserManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.UserManagement;

public class EthnicityEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetEthnicities_WhenNoData_ReturnsEmptyList()
    {
        // Act
        var response = await Client.GetAsync("/api/ethnicities");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ethnicities = await response.Content.ReadFromJsonAsync<List<GetEthnicitiesContract.Response>>();
        Assert.NotNull(ethnicities);
        Assert.Empty(ethnicities);
    }

    [Fact]
    public async Task GetEthnicities_ReturnsEthnicitiesWithRegions()
    {
        // Arrange
        await SeedEthnicitiesAsync();

        // Act
        var response = await Client.GetAsync("/api/ethnicities");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ethnicities = await response.Content.ReadFromJsonAsync<List<GetEthnicitiesContract.Response>>();
        Assert.NotNull(ethnicities);
        Assert.Equal(2, ethnicities.Count);

        var greek = ethnicities.Single(e => e.Name == "Greek");
        Assert.Equal(2, greek.Regions.Count);
        Assert.Contains(greek.Regions, r => r.Name == "Attica");
        Assert.Contains(greek.Regions, r => r.Name == "Peloponnese");

        var albanian = ethnicities.Single(e => e.Name == "Albanian");
        Assert.Single(albanian.Regions);
        Assert.Contains(albanian.Regions, r => r.Name == "South Albanian");
    }

    [Fact]
    public async Task GetEthnicities_EthnicityWithNoRegions_ReturnsEmptyRegionsList()
    {
        // Arrange
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        dbContext.Ethnicities.Add(new Ethnicity { Name = "Pomak" });
        await dbContext.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync("/api/ethnicities");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ethnicities = await response.Content.ReadFromJsonAsync<List<GetEthnicitiesContract.Response>>();
        Assert.NotNull(ethnicities);
        Assert.Single(ethnicities);

        var pomak = ethnicities.Single();
        Assert.Equal("Pomak", pomak.Name);
        Assert.Empty(pomak.Regions);
    }

    [Fact]
    public async Task GetEthnicities_ResponseContainsCorrectIds()
    {
        // Arrange
        await SeedEthnicitiesAsync();

        // Act
        var response = await Client.GetAsync("/api/ethnicities");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ethnicities = await response.Content.ReadFromJsonAsync<List<GetEthnicitiesContract.Response>>();
        Assert.NotNull(ethnicities);
        Assert.All(ethnicities, e =>
        {
            Assert.True(e.Id > 0);
            Assert.All(e.Regions, r => Assert.True(r.Id > 0));
        });
    }

    private async Task SeedEthnicitiesAsync()
    {
        using var scope = CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var greek = new Ethnicity { Name = "Greek" };
        var albanian = new Ethnicity { Name = "Albanian" };
        dbContext.Ethnicities.AddRange(greek, albanian);
        await dbContext.SaveChangesAsync();

        dbContext.Regions.AddRange(
            new Region { Name = "Attica", Ethnicity = greek },
            new Region { Name = "Peloponnese", Ethnicity = greek },
            new Region { Name = "South Albanian", Ethnicity = albanian }
        );
        await dbContext.SaveChangesAsync();
    }
}
