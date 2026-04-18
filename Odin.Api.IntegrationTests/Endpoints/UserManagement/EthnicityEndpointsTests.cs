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
    public async Task GetEthnicities_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/ethnicities");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ethnicities = await response.Content.ReadFromJsonAsync<List<GetEthnicitiesContract.Response>>();
        Assert.NotNull(ethnicities);
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
        dbContext.QpadmEthnicities.Add(new QpadmEthnicity { Name = "Pomak" });
        await dbContext.SaveChangesAsync();

        // Act
        var response = await Client.GetAsync("/api/ethnicities");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var ethnicities = await response.Content.ReadFromJsonAsync<List<GetEthnicitiesContract.Response>>();
        Assert.NotNull(ethnicities);
        var pomak = Assert.Single(ethnicities, e => e.Name == "Pomak");
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

        var greek = new QpadmEthnicity { Name = "Greek" };
        var albanian = new QpadmEthnicity { Name = "Albanian" };
        dbContext.QpadmEthnicities.AddRange(greek, albanian);
        await dbContext.SaveChangesAsync();

        dbContext.QpadmRegions.AddRange(
            new QpadmRegion { Name = "Attica", Ethnicity = greek },
            new QpadmRegion { Name = "Peloponnese", Ethnicity = greek },
            new QpadmRegion { Name = "South Albanian", Ethnicity = albanian }
        );
        await dbContext.SaveChangesAsync();
    }
}
