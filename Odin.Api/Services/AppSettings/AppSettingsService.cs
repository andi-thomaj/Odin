using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Odin.Api.Data;
using Odin.Api.Data.Entities;

namespace Odin.Api.Services.AppSettings;

public sealed class AppSettingsService(
    ApplicationDbContext dbContext,
    IMemoryCache cache,
    ILogger<AppSettingsService> logger) : IAppSettingsService
{
    private const string CachePrefix = "appsetting:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    public async Task<bool> GetBoolAsync(string key, bool defaultValue, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var cacheKey = CachePrefix + key;
        if (cache.TryGetValue<bool?>(cacheKey, out var cached) && cached.HasValue)
            return cached.Value;

        var raw = await dbContext.AppSettings
            .AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(cancellationToken);

        var resolved = ParseBool(raw, defaultValue);
        cache.Set<bool?>(cacheKey, resolved, CacheTtl);
        return resolved;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.AppSettings
            .AsNoTracking()
            .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);
        return rows;
    }

    public async Task SetBoolAsync(string key, bool value, string? updatedBy, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var stored = value ? "true" : "false";
        var existing = await dbContext.AppSettings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (existing is null)
        {
            dbContext.AppSettings.Add(new AppSetting
            {
                Key = key,
                Value = stored,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = updatedBy,
            });
        }
        else
        {
            existing.Value = stored;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = updatedBy;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(CachePrefix + key);
        logger.LogInformation("AppSetting {Key} set to {Value} by {UpdatedBy}.", key, stored, updatedBy ?? "(unknown)");
    }

    private static bool ParseBool(string? raw, bool defaultValue)
    {
        if (string.IsNullOrWhiteSpace(raw)) return defaultValue;
        if (bool.TryParse(raw, out var parsed)) return parsed;
        return defaultValue;
    }
}
