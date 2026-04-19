using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.OrderManagement.Models;
using Odin.Api.IntegrationTests.Fakers;
using Odin.Api.IntegrationTests.Infrastructure;
using static Odin.Api.IntegrationTests.Fakers.TestDataHelper;

namespace Odin.Api.IntegrationTests.Endpoints.OrderManagement;

public class G25OrderEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string DistanceSourceCsv =
        "PopA,0.01,0.02,0.03\n" +
        "PopB,0.1,0.2,0.3\n" +
        "PopC,0.5,0.4,0.3\n" +
        "PopD,1.0,1.0,1.0";

    private const string TargetCoordinates = "TestTarget,0.02,0.03,0.04";

    [Fact]
    public async Task CreateG25_WithCoordinatesAndSeededDistanceFile_MarksCompletedAndPersistsResults()
    {
        var (eraId, _) = await SeedG25EraWithDistanceFileAsync();

        using var content = BuildG25OrderForm(TargetCoordinates);
        var response = await Client.PostAsync("/api/orders", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("g25", created!.Service);
        Assert.Equal("Completed", created.Status);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var inspection = await db.G25GeneticInspections.AsNoTracking().FirstAsync(gi => gi.OrderId == created.Id);
        var results = await db.G25DistanceResults.AsNoTracking()
            .Where(r => r.GeneticInspectionId == inspection.Id)
            .ToListAsync();
        Assert.Single(results);
        Assert.Equal(eraId, results[0].G25DistanceEraId);
        Assert.NotEmpty(results[0].Populations);
        Assert.All(results[0].Populations, p => Assert.False(string.IsNullOrEmpty(p.Name)));
    }

    [Fact]
    public async Task CreateG25_WithCoordinatesButNoEraWithDistanceFile_StaysPendingAndPersistsNoResults()
    {
        using var content = BuildG25OrderForm(TargetCoordinates);
        var response = await Client.PostAsync("/api/orders", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal("Pending", created!.Status);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var inspection = await db.G25GeneticInspections.AsNoTracking().FirstAsync(gi => gi.OrderId == created.Id);
        var results = await db.G25DistanceResults.AsNoTracking()
            .Where(r => r.GeneticInspectionId == inspection.Id)
            .ToListAsync();
        Assert.Empty(results);
    }

    [Fact]
    public async Task GetG25Result_AfterSuccessfulCompute_ReturnsPopulatedDistanceEras()
    {
        var (_, eraName) = await SeedG25EraWithDistanceFileAsync();

        using var content = BuildG25OrderForm(TargetCoordinates);
        var create = await Client.PostAsync("/api/orders", content);
        create.EnsureSuccessStatusCode();
        var created = (await create.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions))!;

        var response = await Client.GetAsync($"/api/orders/{created.Id}/g25-result");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<GetOrderG25ResultContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.Single(body!.DistanceEras);
        Assert.Equal(eraName, body.DistanceEras[0].EraName);
        Assert.NotEmpty(body.DistanceEras[0].Populations);
        Assert.Equal(1, body.DistanceEras[0].Populations[0].Rank);
        Assert.Null(body.Admixture);
        Assert.Empty(body.Pca);
    }

    [Fact]
    public async Task GetG25Result_WhenNoDistanceResultsExist_ReturnsEmptyArrays()
    {
        using var content = BuildG25OrderForm(TargetCoordinates);
        var create = await Client.PostAsync("/api/orders", content);
        create.EnsureSuccessStatusCode();
        var created = (await create.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions))!;

        var response = await Client.GetAsync($"/api/orders/{created.Id}/g25-result");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<GetOrderG25ResultContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.Empty(body!.DistanceEras);
        Assert.Null(body.Admixture);
        Assert.Empty(body.Pca);
    }

    [Fact]
    public async Task GetG25Result_NonExistentOrder_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/orders/99999/g25-result");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetG25Result_OtherUsersOrder_ReturnsForbidden()
    {
        await SeedG25EraWithDistanceFileAsync();

        using var content = BuildG25OrderForm(TargetCoordinates);
        var create = await Client.PostAsync("/api/orders", content);
        create.EnsureSuccessStatusCode();
        var created = (await create.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions))!;

        var otherUser = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", otherUser);
        using var otherClient = CreateClientWithRole(Factory, otherUser.IdentityId, "User");

        var response = await otherClient.GetAsync($"/api/orders/{created.Id}/g25-result");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private async Task<(int EraId, string EraName)> SeedG25EraWithDistanceFileAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        var era = new G25DistanceEra
        {
            Name = $"Era-{Guid.NewGuid():N}",
            CreatedAt = now,
            CreatedBy = "test",
            UpdatedAt = now,
            UpdatedBy = "test"
        };
        db.G25DistanceEras.Add(era);
        await db.SaveChangesAsync();

        db.G25DistanceFiles.Add(new G25DistanceFile
        {
            Title = $"Dist-{Guid.NewGuid():N}",
            Content = DistanceSourceCsv,
            G25DistanceEraId = era.Id,
            CreatedAt = now,
            CreatedBy = "test",
            UpdatedAt = now,
            UpdatedBy = "test"
        });
        await db.SaveChangesAsync();

        return (era.Id, era.Name);
    }

    private static MultipartFormDataContent BuildG25OrderForm(
        string coordinates,
        string firstName = "G25",
        string lastName = "Tester",
        string gender = "Male")
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(firstName), "FirstName" },
            { new StringContent(lastName), "LastName" },
            { new StringContent(gender), "Gender" },
            { new StringContent(((int)ServiceType.g25).ToString()), "Service" },
            { new StringContent(coordinates), "G25Coordinates" }
        };
        return content;
    }
}
