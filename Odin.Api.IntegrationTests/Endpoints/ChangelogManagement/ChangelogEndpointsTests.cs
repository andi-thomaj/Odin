using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.ChangelogManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.ChangelogManagement;

public class ChangelogEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private async Task SeedChangelogAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        db.ChangelogVersions.Add(new ChangelogVersion
        {
            Version = "1.0.0",
            Title = "First",
            ReleasedAt = now,
            IsPublished = true,
            CreatedAt = now,
            CreatedBy = "seed",
            UpdatedAt = now,
            UpdatedBy = "seed",
            Entries =
            [
                new ChangelogEntry
                {
                    Type = "Feature",
                    Description = "Hello",
                    DisplayOrder = 0,
                    CreatedAt = now,
                    CreatedBy = "seed",
                    UpdatedAt = now,
                    UpdatedBy = "seed"
                }
            ]
        });

        db.ChangelogVersions.Add(new ChangelogVersion
        {
            Version = "0.9.0",
            Title = "Draft only",
            ReleasedAt = now.AddDays(-10),
            IsPublished = false,
            CreatedAt = now,
            CreatedBy = "seed",
            UpdatedAt = now,
            UpdatedBy = "seed"
        });

        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GetChangelog_ReturnsOnlyPublishedVersions()
    {
        await SeedChangelogAsync();

        var response = await Client.GetAsync("/api/changelog");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<List<GetChangelogContract.VersionResponse>>();
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal("1.0.0", list![0].Version);
        Assert.Single(list[0].Entries);
        Assert.Equal("Hello", list[0].Entries[0].Description);
    }

    [Fact]
    public async Task GetChangelogAll_AsAdmin_ReturnsAllVersions()
    {
        await SeedChangelogAsync();

        var response = await Client.GetAsync("/api/changelog/all");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var list = await response.Content.ReadFromJsonAsync<List<GetChangelogContract.VersionResponse>>();
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
    }

    [Fact]
    public async Task GetChangelogAll_AsUser_ReturnsForbidden()
    {
        await SeedChangelogAsync();

        Client.DefaultRequestHeaders.Remove("X-Test-App-Role");
        Client.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-App-Role", "User");

        var response = await Client.GetAsync("/api/changelog/all");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostVersion_AsAdmin_CreatesVersion()
    {
        var body = new CreateVersionContract.Request
        {
            Version = "2.0.0",
            Title = "New release",
            ReleasedAt = DateTime.UtcNow,
            IsPublished = true
        };

        var response = await Client.PostAsJsonAsync("/api/changelog/versions", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await response.Content.ReadFromJsonAsync<CreateVersionContract.Response>();
        Assert.NotNull(created);
        Assert.Equal("2.0.0", created!.Version);

        var get = await Client.GetAsync("/api/changelog");
        var list = await get.Content.ReadFromJsonAsync<List<GetChangelogContract.VersionResponse>>();
        Assert.NotNull(list);
        Assert.Contains(list!, v => v.Version == "2.0.0");
    }
}
