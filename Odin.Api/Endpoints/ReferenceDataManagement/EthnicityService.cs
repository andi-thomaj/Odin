using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.ReferenceDataManagement.Models;
using Odin.Api.Endpoints.UserManagement.Models;

namespace Odin.Api.Endpoints.ReferenceDataManagement;

public interface IEthnicityService
{
    Task<IEnumerable<GetEthnicitiesContract.Response>> GetAllAsync();

    Task<IReadOnlyList<GetEthnicityAdminContract.Response>> GetAllAdminAsync(CancellationToken cancellationToken = default);
    Task<GetEthnicityAdminContract.Response?> GetByIdAdminAsync(int id, CancellationToken cancellationToken = default);
    Task<(GetEthnicityAdminContract.Response? Response, string? Error)> CreateEthnicityAsync(CreateEthnicityContract.Request request, CancellationToken cancellationToken = default);
    Task<(GetEthnicityAdminContract.Response? Response, string? Error, bool NotFound)> UpdateEthnicityAsync(int id, UpdateEthnicityContract.Request request, CancellationToken cancellationToken = default);
    Task<bool> DeleteEthnicityAsync(int id, CancellationToken cancellationToken = default);

    Task<(GetEthnicityAdminContract.RegionItem? Region, string? Error, bool EthnicityNotFound)> CreateRegionAsync(int ethnicityId, CreateRegionContract.Request request, CancellationToken cancellationToken = default);
    Task<(GetEthnicityAdminContract.RegionItem? Region, string? Error, bool NotFound)> UpdateRegionAsync(int ethnicityId, int regionId, UpdateRegionContract.Request request, CancellationToken cancellationToken = default);
    Task<bool> DeleteRegionAsync(int ethnicityId, int regionId, CancellationToken cancellationToken = default);
}

public class EthnicityService(
    ApplicationDbContext dbContext,
    IMemoryCache cache,
    IHostEnvironment hostEnvironment) : IEthnicityService
{
    private const string CacheKey = "AllEthnicities";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    public async Task<IEnumerable<GetEthnicitiesContract.Response>> GetAllAsync()
    {
        if (!hostEnvironment.IsEnvironment("Testing") &&
            cache.TryGetValue(CacheKey, out List<GetEthnicitiesContract.Response>? cached))
            return cached!;

        var result = await dbContext.QpadmEthnicities
            .AsNoTracking()
            .Include(e => e.Regions)
            .Select(e => new GetEthnicitiesContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                Regions = e.Regions.Select(r => new GetEthnicitiesContract.RegionItem
                {
                    Id = r.Id, Name = r.Name
                }).ToList()
            })
            .ToListAsync();

        if (!hostEnvironment.IsEnvironment("Testing"))
        {
            cache.Set(CacheKey, result, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = CacheDuration
            });
        }

        return result;
    }

    public async Task<IReadOnlyList<GetEthnicityAdminContract.Response>> GetAllAdminAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.QpadmEthnicities
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .Select(e => new GetEthnicityAdminContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                Regions = e.Regions
                    .OrderBy(r => r.Name)
                    .Select(r => new GetEthnicityAdminContract.RegionItem { Id = r.Id, Name = r.Name })
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<GetEthnicityAdminContract.Response?> GetByIdAdminAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.QpadmEthnicities
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new GetEthnicityAdminContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                Regions = e.Regions
                    .OrderBy(r => r.Name)
                    .Select(r => new GetEthnicityAdminContract.RegionItem { Id = r.Id, Name = r.Name })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(GetEthnicityAdminContract.Response? Response, string? Error)> CreateEthnicityAsync(
        CreateEthnicityContract.Request request, CancellationToken cancellationToken = default)
    {
        var error = await ValidateEthnicityNameAsync(request.Name, existingId: null, cancellationToken);
        if (error is not null) return (null, error);

        var entity = new QpadmEthnicity { Name = request.Name.Trim() };
        dbContext.QpadmEthnicities.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCache();

        var response = await GetByIdAdminAsync(entity.Id, cancellationToken);
        return (response, null);
    }

    public async Task<(GetEthnicityAdminContract.Response? Response, string? Error, bool NotFound)> UpdateEthnicityAsync(
        int id, UpdateEthnicityContract.Request request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.QpadmEthnicities.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null) return (null, null, true);

        var error = await ValidateEthnicityNameAsync(request.Name, existingId: id, cancellationToken);
        if (error is not null) return (null, error, false);

        entity.Name = request.Name.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCache();

        var response = await GetByIdAdminAsync(entity.Id, cancellationToken);
        return (response, null, false);
    }

    public async Task<bool> DeleteEthnicityAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.QpadmEthnicities
            .Include(e => e.Regions)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null) return false;

        if (entity.Regions.Count > 0) dbContext.QpadmRegions.RemoveRange(entity.Regions);
        dbContext.QpadmEthnicities.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCache();
        return true;
    }

    public async Task<(GetEthnicityAdminContract.RegionItem? Region, string? Error, bool EthnicityNotFound)> CreateRegionAsync(
        int ethnicityId, CreateRegionContract.Request request, CancellationToken cancellationToken = default)
    {
        var ethnicity = await dbContext.QpadmEthnicities.FirstOrDefaultAsync(e => e.Id == ethnicityId, cancellationToken);
        if (ethnicity is null) return (null, null, true);

        var error = await ValidateRegionNameAsync(ethnicityId, request.Name, existingId: null, cancellationToken);
        if (error is not null) return (null, error, false);

        var region = new QpadmRegion { Name = request.Name.Trim(), EthnicityId = ethnicityId, Ethnicity = ethnicity };
        dbContext.QpadmRegions.Add(region);
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCache();

        return (new GetEthnicityAdminContract.RegionItem { Id = region.Id, Name = region.Name }, null, false);
    }

    public async Task<(GetEthnicityAdminContract.RegionItem? Region, string? Error, bool NotFound)> UpdateRegionAsync(
        int ethnicityId, int regionId, UpdateRegionContract.Request request, CancellationToken cancellationToken = default)
    {
        var region = await dbContext.QpadmRegions
            .FirstOrDefaultAsync(r => r.Id == regionId && r.EthnicityId == ethnicityId, cancellationToken);
        if (region is null) return (null, null, true);

        var error = await ValidateRegionNameAsync(ethnicityId, request.Name, existingId: regionId, cancellationToken);
        if (error is not null) return (null, error, false);

        region.Name = request.Name.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCache();

        return (new GetEthnicityAdminContract.RegionItem { Id = region.Id, Name = region.Name }, null, false);
    }

    public async Task<bool> DeleteRegionAsync(int ethnicityId, int regionId, CancellationToken cancellationToken = default)
    {
        var region = await dbContext.QpadmRegions
            .FirstOrDefaultAsync(r => r.Id == regionId && r.EthnicityId == ethnicityId, cancellationToken);
        if (region is null) return false;

        dbContext.QpadmRegions.Remove(region);
        await dbContext.SaveChangesAsync(cancellationToken);
        InvalidateCache();
        return true;
    }

    private async Task<string?> ValidateEthnicityNameAsync(string name, int? existingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            return "Name is required and must be 1–100 characters.";

        var trimmed = name.Trim();
        var nameExists = await dbContext.QpadmEthnicities
            .AsNoTracking()
            .AnyAsync(e => e.Name == trimmed && (existingId == null || e.Id != existingId), cancellationToken);
        if (nameExists) return $"An ethnicity named '{trimmed}' already exists.";

        return null;
    }

    private async Task<string?> ValidateRegionNameAsync(int ethnicityId, string name, int? existingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            return "Name is required and must be 1–100 characters.";

        var trimmed = name.Trim();
        var nameExists = await dbContext.QpadmRegions
            .AsNoTracking()
            .AnyAsync(r => r.EthnicityId == ethnicityId && r.Name == trimmed && (existingId == null || r.Id != existingId), cancellationToken);
        if (nameExists) return $"A region named '{trimmed}' already exists for this ethnicity.";

        return null;
    }

    private void InvalidateCache() => cache.Remove(CacheKey);
}
