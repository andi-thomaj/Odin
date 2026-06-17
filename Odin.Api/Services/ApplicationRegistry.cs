using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Odin.Api.Data;
using Odin.Api.Data.Entities;

namespace Odin.Api.Services;

/// <summary>
/// Read-through cache over the <c>applications</c> registry table. Used by
/// <see cref="Middleware.AppResolutionMiddleware"/> to validate the <c>X-App</c> header, and by email/redirect
/// code to resolve per-app branding (frontend URL, from-email). The set is tiny and changes rarely, so it is
/// cached whole with a short TTL — skipped under the <c>Testing</c> environment so integration tests read fresh
/// after Respawn re-seeds. Keys are matched case-insensitively.
/// </summary>
public interface IApplicationRegistry
{
    /// <summary>All active applications keyed by <see cref="Application.Key"/> (case-insensitive).</summary>
    Task<IReadOnlyDictionary<string, Application>> GetActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>The active registry row for <paramref name="key"/>, or <c>null</c> if unknown/inactive.</summary>
    Task<Application?> GetActiveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>Drops the cached set (call after seeding/editing the <c>applications</c> table).</summary>
    void Invalidate();
}

public class ApplicationRegistry(
    ApplicationDbContext dbContext,
    IMemoryCache cache,
    IHostEnvironment hostEnvironment) : IApplicationRegistry
{
    private const string CacheKey = "AppRegistry:Active:v1";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(30);

    public async Task<IReadOnlyDictionary<string, Application>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        if (!hostEnvironment.IsEnvironment("Testing") &&
            cache.TryGetValue(CacheKey, out IReadOnlyDictionary<string, Application>? cached) && cached is not null)
            return cached;

        var rows = await dbContext.Applications
            .AsNoTracking()
            .Where(a => a.IsActive)
            .ToListAsync(cancellationToken);

        var result = (IReadOnlyDictionary<string, Application>)rows
            .ToDictionary(a => a.Key, a => a, StringComparer.OrdinalIgnoreCase);

        if (!hostEnvironment.IsEnvironment("Testing"))
        {
            cache.Set(CacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration
            });
        }

        return result;
    }

    public async Task<Application?> GetActiveAsync(string key, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(key))
            return null;
        var map = await GetActiveAsync(cancellationToken);
        return map.TryGetValue(key, out var app) ? app : null;
    }

    public void Invalidate() => cache.Remove(CacheKey);
}
