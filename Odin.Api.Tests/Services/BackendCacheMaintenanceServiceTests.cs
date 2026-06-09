using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Odin.Api.Services;

namespace Odin.Api.Tests.Services;

public class BackendCacheMaintenanceServiceTests
{
    [Fact]
    public void ClearAll_EvictsEveryEntry_AndReturnsEvictedCount()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        cache.Set("order-result:qpadm:1", "payload-1");
        cache.Set("order-result:g25:1", "payload-2");
        cache.Set("AllEras", "payload-3");

        var service = new BackendCacheMaintenanceService(
            cache, NullLogger<BackendCacheMaintenanceService>.Instance);

        var cleared = service.ClearAll();

        Assert.Equal(3L, cleared);
        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGetValue("order-result:qpadm:1", out _));
        Assert.False(cache.TryGetValue("order-result:g25:1", out _));
        Assert.False(cache.TryGetValue("AllEras", out _));
    }

    [Fact]
    public void ClearAll_OnEmptyCache_ReturnsZero()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new BackendCacheMaintenanceService(
            cache, NullLogger<BackendCacheMaintenanceService>.Instance);

        Assert.Equal(0L, service.ClearAll());
    }

    [Fact]
    public void ClearAll_WhenCacheImplementationCannotClear_ReturnsMinusOne_AndDoesNotThrow()
    {
        var service = new BackendCacheMaintenanceService(
            new NonClearableMemoryCache(), NullLogger<BackendCacheMaintenanceService>.Instance);

        Assert.Equal(-1L, service.ClearAll());
    }

    // An IMemoryCache that is deliberately NOT a MemoryCache, to exercise the defensive no-op path.
    private sealed class NonClearableMemoryCache : IMemoryCache
    {
        public ICacheEntry CreateEntry(object key) => throw new NotSupportedException();
        public void Remove(object key) { }
        public bool TryGetValue(object key, out object? value)
        {
            value = null;
            return false;
        }
        public void Dispose() { }
    }
}
