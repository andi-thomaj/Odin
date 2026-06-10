using Microsoft.Extensions.Caching.Memory;

namespace Odin.Api.Services;

public interface IBackendCacheMaintenanceService
{
    /// <summary>
    /// Evicts every entry from the in-process <see cref="IMemoryCache"/> — qpAdm/G25 result payloads,
    /// reference-data lookups (eras, ethnicities, populations, G25 distance population samples),
    /// geo-location and auth/token caches, etc.
    /// Returns the number of entries evicted, or <c>-1</c> when the registered cache implementation does
    /// not support counting/clearing (in which case the call is a logged no-op).
    /// </summary>
    long ClearAll();
}

public class BackendCacheMaintenanceService(
    IMemoryCache cache,
    ILogger<BackendCacheMaintenanceService> logger) : IBackendCacheMaintenanceService
{
    public long ClearAll()
    {
        // AddMemoryCache() registers the concrete MemoryCache, which (unlike the IMemoryCache
        // interface) exposes Count + Clear. Guard the cast so a swapped implementation degrades to a
        // logged no-op instead of throwing.
        if (cache is not MemoryCache memoryCache)
        {
            logger.LogWarning(
                "Backend cache clear requested but the registered IMemoryCache is {Type}, which does not support Clear(); no-op.",
                cache.GetType().FullName);
            return -1;
        }

        var evicted = memoryCache.Count;
        memoryCache.Clear();
        logger.LogInformation("Backend cache cleared by admin: {Count} entries evicted.", evicted);
        return evicted;
    }
}
