using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.AncestralPortraitManagement.Models;

namespace Odin.Api.Endpoints.AncestralPortraitManagement;

/// <summary>Admin-editable AI-portrait settings (single row, memory-cached). Mirrors <c>ImageSettingsService</c>.</summary>
public interface IAncestralPortraitSettingsService
{
    Task<AncestralPortraitSettingsContract.Response> GetAsync(CancellationToken cancellationToken = default);
    Task<AncestralPortraitSettingsContract.Response> UpdateAsync(
        AncestralPortraitSettingsContract.Request request, string identityId, CancellationToken cancellationToken = default);
}

public sealed class AncestralPortraitSettingsService(
    ApplicationDbContext dbContext,
    IMemoryCache cache,
    IHostEnvironment environment,
    ILogger<AncestralPortraitSettingsService> logger) : IAncestralPortraitSettingsService
{
    private const string CacheKey = "ancestral-portrait-settings";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const int SingletonId = 1;

    private bool UseCache => !environment.IsEnvironment("Testing");

    public async Task<AncestralPortraitSettingsContract.Response> GetAsync(CancellationToken cancellationToken = default)
    {
        if (UseCache && cache.TryGetValue<AncestralPortraitSettingsContract.Response>(CacheKey, out var cached) && cached is not null)
            return cached;

        var entity = await GetOrSeedAsync(cancellationToken);
        var response = Map(entity);
        if (UseCache)
            cache.Set(CacheKey, response, CacheTtl);
        return response;
    }

    public async Task<AncestralPortraitSettingsContract.Response> UpdateAsync(
        AncestralPortraitSettingsContract.Request request, string identityId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AncestralPortraitSettings.FirstOrDefaultAsync(s => s.Id == SingletonId, cancellationToken);
        var now = DateTime.UtcNow;
        if (entity is null)
        {
            entity = new AncestralPortraitSettings { Id = SingletonId, CreatedBy = identityId, CreatedAt = now };
            dbContext.AncestralPortraitSettings.Add(entity);
        }

        entity.Model = request.Model;
        entity.Size = request.Size;
        entity.Quality = request.Quality;
        entity.Background = request.Background;
        entity.OutputFormat = request.OutputFormat;
        entity.Moderation = request.Moderation;
        entity.VariationsPerEra = request.VariationsPerEra;
        entity.MaxEras = request.MaxEras;
        entity.MaxPopulationsPerEra = request.MaxPopulationsPerEra;
        entity.MaxFaceReferences = request.MaxFaceReferences;
        entity.CostPerMillionInputTokensUsd = request.CostPerMillionInputTokensUsd;
        entity.CostPerMillionOutputTokensUsd = request.CostPerMillionOutputTokensUsd;
        entity.UpdatedAt = now;
        entity.UpdatedBy = identityId;

        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
        logger.LogInformation("Ancestral portrait settings updated by {IdentityId}.", identityId);
        return Map(entity);
    }

    private async Task<AncestralPortraitSettings> GetOrSeedAsync(CancellationToken cancellationToken)
    {
        var entity = await dbContext.AncestralPortraitSettings.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == SingletonId, cancellationToken);
        if (entity is not null)
            return entity;

        var now = DateTime.UtcNow;
        var seeded = new AncestralPortraitSettings { Id = SingletonId, CreatedBy = "system", CreatedAt = now, UpdatedAt = now };
        dbContext.AncestralPortraitSettings.Add(seeded);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.Entry(seeded).State = EntityState.Detached;
            return await dbContext.AncestralPortraitSettings.AsNoTracking().FirstAsync(s => s.Id == SingletonId, cancellationToken);
        }
        return seeded;
    }

    private static AncestralPortraitSettingsContract.Response Map(AncestralPortraitSettings e) => new()
    {
        Model = e.Model,
        Size = e.Size,
        Quality = e.Quality,
        Background = e.Background,
        OutputFormat = e.OutputFormat,
        Moderation = e.Moderation,
        VariationsPerEra = e.VariationsPerEra,
        MaxEras = e.MaxEras,
        MaxPopulationsPerEra = e.MaxPopulationsPerEra,
        MaxFaceReferences = e.MaxFaceReferences,
        CostPerMillionInputTokensUsd = e.CostPerMillionInputTokensUsd,
        CostPerMillionOutputTokensUsd = e.CostPerMillionOutputTokensUsd,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
    };
}
