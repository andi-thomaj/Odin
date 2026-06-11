using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.QpadmPopulationPanelSampleManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.QpadmPopulationPanelSampleManagement;

/// <summary>
/// CRUD coverage for linking merge-panel samples (by stable <c>.ind</c> sample id) to
/// <c>QpadmPopulation</c>s via <c>api/qpadm-population-panel-samples</c>. Each test acts as its own
/// scientist identity so the per-user "strict" rate-limit window never couples tests.
/// </summary>
public class QpadmPopulationPanelSampleEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string Panel = "HO";
    private const string Route = "/api/qpadm-population-panel-samples";

    /// <summary>Seeds the reference catalog and returns a handful of population ids to link against.</summary>
    private async Task<List<int>> SeedPopulationsAsync()
    {
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedReferenceCatalogAsync();
        }

        var db = await GetDbContextAsync();
        return await db.QpadmPopulations.OrderBy(p => p.Id).Select(p => p.Id).Take(3).ToListAsync();
    }

    private Task<HttpClient> ScientistClientAsync(string suffix) =>
        CreateClientAsAsync($"auth0|panel-link-{suffix}", AppRole.Scientist);

    private static async Task<List<GetPanelSampleLinksContract.Response>> GetLinksAsync(HttpClient client)
    {
        var response = await client.GetAsync($"{Route}?panel={Panel}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var links = await response.Content.ReadFromJsonAsync<List<GetPanelSampleLinksContract.Response>>();
        Assert.NotNull(links);
        return links;
    }

    // ── GET ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetLinks_WhenNoneExist_ReturnsEmpty()
    {
        var client = await ScientistClientAsync("get-empty");

        var links = await GetLinksAsync(client);

        Assert.Empty(links);
    }

    [Fact]
    public async Task GetLinks_WithoutPanel_ReturnsBadRequest()
    {
        var client = await ScientistClientAsync("get-no-panel");

        var response = await client.GetAsync(Route);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── PUT /sample (set) ──────────────────────────────────────────

    [Fact]
    public async Task SetSamplePopulations_AddsLinks_AndGetReflectsThem()
    {
        var populationIds = await SeedPopulationsAsync();
        var client = await ScientistClientAsync("set-add");

        var response = await client.PutAsJsonAsync($"{Route}/sample", new SetSamplePopulationsContract.Request
        {
            Panel = Panel,
            SampleId = "HO.001",
            PopulationIds = [populationIds[0], populationIds[1]],
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SetSamplePopulationsContract.Response>();
        Assert.NotNull(body);
        Assert.Equal(2, body.Populations.Count);
        Assert.Equal([populationIds[0], populationIds[1]], body.Populations.Select(p => p.PopulationId).OrderBy(id => id));

        var links = await GetLinksAsync(client);
        Assert.Equal(2, links.Count);
        Assert.All(links, l => Assert.Equal("HO.001", l.SampleId));
    }

    [Fact]
    public async Task SetSamplePopulations_Replace_RemovesMissingAndAddsNew()
    {
        var populationIds = await SeedPopulationsAsync();
        var client = await ScientistClientAsync("set-replace");

        await client.PutAsJsonAsync($"{Route}/sample", new SetSamplePopulationsContract.Request
        {
            Panel = Panel,
            SampleId = "HO.002",
            PopulationIds = [populationIds[0], populationIds[1]],
        });

        // Replace {0,1} with {1,2} — drops 0, keeps 1, adds 2.
        var response = await client.PutAsJsonAsync($"{Route}/sample", new SetSamplePopulationsContract.Request
        {
            Panel = Panel,
            SampleId = "HO.002",
            PopulationIds = [populationIds[1], populationIds[2]],
        });

        var body = await response.Content.ReadFromJsonAsync<SetSamplePopulationsContract.Response>();
        Assert.NotNull(body);
        Assert.Equal([populationIds[1], populationIds[2]], body.Populations.Select(p => p.PopulationId).OrderBy(id => id));
    }

    [Fact]
    public async Task SetSamplePopulations_EmptyList_ClearsAllLinks()
    {
        var populationIds = await SeedPopulationsAsync();
        var client = await ScientistClientAsync("set-clear");

        await client.PutAsJsonAsync($"{Route}/sample", new SetSamplePopulationsContract.Request
        {
            Panel = Panel,
            SampleId = "HO.003",
            PopulationIds = [populationIds[0]],
        });

        var response = await client.PutAsJsonAsync($"{Route}/sample", new SetSamplePopulationsContract.Request
        {
            Panel = Panel,
            SampleId = "HO.003",
            PopulationIds = [],
        });

        var body = await response.Content.ReadFromJsonAsync<SetSamplePopulationsContract.Response>();
        Assert.NotNull(body);
        Assert.Empty(body.Populations);
        Assert.Empty(await GetLinksAsync(client));
    }

    [Fact]
    public async Task SetSamplePopulations_InvalidPopulationId_IsIgnored()
    {
        var populationIds = await SeedPopulationsAsync();
        var client = await ScientistClientAsync("set-invalid");

        var response = await client.PutAsJsonAsync($"{Route}/sample", new SetSamplePopulationsContract.Request
        {
            Panel = Panel,
            SampleId = "HO.004",
            PopulationIds = [populationIds[0], 999_999],
        });

        var body = await response.Content.ReadFromJsonAsync<SetSamplePopulationsContract.Response>();
        Assert.NotNull(body);
        Assert.Equal([populationIds[0]], body.Populations.Select(p => p.PopulationId));
    }

    [Fact]
    public async Task SetSamplePopulations_RepeatedSameSet_DoesNotDuplicate()
    {
        var populationIds = await SeedPopulationsAsync();
        var client = await ScientistClientAsync("set-idempotent");

        var request = new SetSamplePopulationsContract.Request
        {
            Panel = Panel,
            SampleId = "HO.005",
            PopulationIds = [populationIds[0], populationIds[1]],
        };

        await client.PutAsJsonAsync($"{Route}/sample", request);
        await client.PutAsJsonAsync($"{Route}/sample", request);

        var db = await GetDbContextAsync();
        var count = await db.QpadmPopulationPanelSamples.CountAsync(e => e.SampleId == "HO.005");
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Links_AreManyToMany_AcrossSamplesAndPopulations()
    {
        var populationIds = await SeedPopulationsAsync();
        var client = await ScientistClientAsync("many-to-many");

        // One sample → many populations.
        await client.PutAsJsonAsync($"{Route}/sample", new SetSamplePopulationsContract.Request
        {
            Panel = Panel,
            SampleId = "HO.010",
            PopulationIds = [populationIds[0], populationIds[1]],
        });
        // One population (populationIds[0]) → a second sample.
        await client.PutAsJsonAsync($"{Route}/sample", new SetSamplePopulationsContract.Request
        {
            Panel = Panel,
            SampleId = "HO.011",
            PopulationIds = [populationIds[0]],
        });

        var links = await GetLinksAsync(client);
        Assert.Equal(2, links.Count(l => l.SampleId == "HO.010"));
        Assert.Equal(2, links.Count(l => l.PopulationId == populationIds[0]));
    }

    // ── POST /bulk ─────────────────────────────────────────────────

    [Fact]
    public async Task BulkAssign_AddMode_LinksPopulationsToEverySample()
    {
        var populationIds = await SeedPopulationsAsync();
        var client = await ScientistClientAsync("bulk-add");

        var response = await client.PostAsJsonAsync($"{Route}/bulk", new BulkAssignSamplePopulationsContract.Request
        {
            Panel = Panel,
            SampleIds = ["HO.020", "HO.021", "HO.022"],
            PopulationIds = [populationIds[0]],
            Mode = "add",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BulkAssignSamplePopulationsContract.Response>();
        Assert.NotNull(body);
        Assert.Equal(3, body.SamplesAffected);
        Assert.Equal(3, body.LinksAdded);

        var links = await GetLinksAsync(client);
        Assert.Equal(3, links.Count);
        Assert.All(links, l => Assert.Equal(populationIds[0], l.PopulationId));
    }

    [Fact]
    public async Task BulkAssign_ReplaceMode_OverwritesEachSampleSet()
    {
        var populationIds = await SeedPopulationsAsync();
        var client = await ScientistClientAsync("bulk-replace");

        await client.PutAsJsonAsync($"{Route}/sample", new SetSamplePopulationsContract.Request
        {
            Panel = Panel,
            SampleId = "HO.030",
            PopulationIds = [populationIds[0], populationIds[1]],
        });

        var response = await client.PostAsJsonAsync($"{Route}/bulk", new BulkAssignSamplePopulationsContract.Request
        {
            Panel = Panel,
            SampleIds = ["HO.030"],
            PopulationIds = [populationIds[2]],
            Mode = "replace",
        });

        var body = await response.Content.ReadFromJsonAsync<BulkAssignSamplePopulationsContract.Response>();
        Assert.NotNull(body);
        Assert.Equal(2, body.LinksRemoved);
        Assert.Equal(1, body.LinksAdded);

        var links = await GetLinksAsync(client);
        Assert.Single(links);
        Assert.Equal(populationIds[2], links[0].PopulationId);
    }

    [Fact]
    public async Task BulkAssign_AddMode_IsIdempotent()
    {
        var populationIds = await SeedPopulationsAsync();
        var client = await ScientistClientAsync("bulk-idempotent");

        var request = new BulkAssignSamplePopulationsContract.Request
        {
            Panel = Panel,
            SampleIds = ["HO.040"],
            PopulationIds = [populationIds[0]],
            Mode = "add",
        };

        await client.PostAsJsonAsync($"{Route}/bulk", request);
        var second = await client.PostAsJsonAsync($"{Route}/bulk", request);

        var body = await second.Content.ReadFromJsonAsync<BulkAssignSamplePopulationsContract.Response>();
        Assert.NotNull(body);
        Assert.Equal(0, body.LinksAdded);

        Assert.Single(await GetLinksAsync(client));
    }

    // ── AuthZ ──────────────────────────────────────────────────────

    [Fact]
    public async Task SetSamplePopulations_AsRegularUser_IsForbidden()
    {
        await SeedPopulationsAsync();
        var client = await CreateClientAsAsync("auth0|panel-link-regular", AppRole.User);

        var response = await client.PutAsJsonAsync($"{Route}/sample", new SetSamplePopulationsContract.Request
        {
            Panel = Panel,
            SampleId = "HO.050",
            PopulationIds = [],
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
