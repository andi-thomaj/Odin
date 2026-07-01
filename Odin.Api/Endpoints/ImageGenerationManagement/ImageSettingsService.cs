using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.ImageGenerationManagement.Models;

namespace Odin.Api.Endpoints.ImageGenerationManagement;

public sealed class ImageSettingsService(
    ApplicationDbContext dbContext,
    IMemoryCache cache,
    IHostEnvironment environment,
    ILogger<ImageSettingsService> logger) : IImageSettingsService
{
    private const string CacheKey = "image-generation-settings";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);
    private const int SingletonId = 1;

    private bool UseCache => !environment.IsEnvironment("Testing");

    public async Task<ImageGenerationSettingsContract.Response> GetAsync(CancellationToken cancellationToken = default)
    {
        if (UseCache && cache.TryGetValue<ImageGenerationSettingsContract.Response>(CacheKey, out var cached) && cached is not null)
            return cached;

        var entity = await GetOrSeedAsync(cancellationToken);
        var response = Map(entity);

        if (UseCache)
            cache.Set(CacheKey, response, CacheTtl);

        return response;
    }

    public async Task<ImageGenerationSettingsContract.Response> UpdateAsync(
        ImageGenerationSettingsContract.Request request, string identityId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ImageGenerationSettings
            .FirstOrDefaultAsync(s => s.Id == SingletonId, cancellationToken);

        var now = DateTime.UtcNow;
        if (entity is null)
        {
            entity = new ImageGenerationSettings { Id = SingletonId, CreatedBy = identityId, CreatedAt = now };
            dbContext.ImageGenerationSettings.Add(entity);
        }

        entity.Size = request.Size;
        entity.Quality = request.Quality;
        entity.Background = request.Background;
        entity.OutputFormat = request.OutputFormat;
        entity.OutputCompression = request.OutputCompression;
        entity.Moderation = request.Moderation;
        entity.DefaultN = request.DefaultN;
        entity.UpdatedAt = now;
        entity.UpdatedBy = identityId;

        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(CacheKey);
        logger.LogInformation("Image generation settings updated by {IdentityId}.", identityId);

        return Map(entity);
    }

    private async Task<ImageGenerationSettings> GetOrSeedAsync(CancellationToken cancellationToken)
    {
        var entity = await dbContext.ImageGenerationSettings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == SingletonId, cancellationToken);
        if (entity is not null)
            return entity;

        var now = DateTime.UtcNow;
        var seeded = new ImageGenerationSettings { Id = SingletonId, CreatedBy = "system", CreatedAt = now, UpdatedAt = now };
        dbContext.ImageGenerationSettings.Add(seeded);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Lost a race to another first-read; the row now exists — load it.
            dbContext.Entry(seeded).State = EntityState.Detached;
            return await dbContext.ImageGenerationSettings.AsNoTracking()
                .FirstAsync(s => s.Id == SingletonId, cancellationToken);
        }

        return seeded;
    }

    private static ImageGenerationSettingsContract.Response Map(ImageGenerationSettings e) => new()
    {
        Model = e.Model,
        Size = e.Size,
        Quality = e.Quality,
        Background = e.Background,
        OutputFormat = e.OutputFormat,
        OutputCompression = e.OutputCompression,
        Moderation = e.Moderation,
        DefaultN = e.DefaultN,
        UpdatedAt = e.UpdatedAt,
        UpdatedBy = e.UpdatedBy,
    };
}
