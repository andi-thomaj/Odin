using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.Admin.Models;
using Odin.Api.IntegrationTests.Infrastructure;
using static Odin.Api.IntegrationTests.Fakers.TestDataHelper;

namespace Odin.Api.IntegrationTests.Endpoints.Admin;

public class CacheAdminEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string ClearCacheUrl = "/api/admin/cache/clear";

    [Fact]
    public async Task ClearCache_AsAdmin_ReturnsOkAndEntriesClearedCount()
    {
        var response = await Client.PostAsync(ClearCacheUrl, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClearBackendCacheContract.Response>(JsonOptions);
        Assert.NotNull(body);
        // The in-process MemoryCache supports counting, so we get a real (non-negative) eviction count.
        Assert.True(body!.EntriesCleared >= 0);
    }

    [Fact]
    public async Task ClearCache_EvictsExistingEntriesFromTheSharedMemoryCache()
    {
        // The endpoint clears the application's singleton IMemoryCache, so seed a sentinel through
        // that same instance, hit the endpoint, and confirm it's gone.
        const string sentinelKey = "cache-admin-test:sentinel";
        var cache = Factory.Services.GetRequiredService<IMemoryCache>();
        cache.Set(sentinelKey, "value");
        Assert.True(cache.TryGetValue(sentinelKey, out _));

        var response = await Client.PostAsync(ClearCacheUrl, null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ClearBackendCacheContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.True(body!.EntriesCleared >= 1); // at least the sentinel was evicted
        Assert.False(cache.TryGetValue(sentinelKey, out _));
    }

    [Fact]
    public async Task ClearCache_AsNonAdmin_IsForbidden()
    {
        var userClient = await CreateClientAsAsync("auth0|cache-plain-user", AppRole.User);

        var response = await userClient.PostAsync(ClearCacheUrl, null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
