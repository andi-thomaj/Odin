using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25EthnicityManagement.Models;

namespace Odin.Api.Endpoints.G25EthnicityManagement;

public interface IG25EthnicityService
{
    Task<IReadOnlyList<GetG25EthnicityContract.Response>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<GetG25EthnicityContract.Response>> GetByContinentIdAsync(int g25ContinentId, CancellationToken ct = default);
    Task<IReadOnlyList<GetG25EthnicityAdminContract.Response>> GetAllAdminAsync(CancellationToken ct = default);
    Task<GetG25EthnicityAdminContract.Response?> GetByIdAdminAsync(int id, CancellationToken ct = default);
    Task<(GetG25EthnicityAdminContract.Response? Response, string? Error)> CreateAsync(CreateG25EthnicityContract.Request request, CancellationToken ct = default);
    Task<(GetG25EthnicityAdminContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(int id, UpdateG25EthnicityContract.Request request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class G25EthnicityService(ApplicationDbContext dbContext) : IG25EthnicityService
{
    public async Task<IReadOnlyList<GetG25EthnicityContract.Response>> GetAllAsync(CancellationToken ct = default)
    {
        return await dbContext.G25Ethnicities
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .Select(e => new GetG25EthnicityContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                G25ContinentId = e.G25ContinentId,
                G25ContinentName = e.G25Continent.Name,
                Regions = e.G25Regions
                    .OrderBy(r => r.Name)
                    .Select(r => new GetG25EthnicityContract.RegionSummary
                    {
                        Id = r.Id,
                        Name = r.Name
                    })
                    .ToList()
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<GetG25EthnicityContract.Response>> GetByContinentIdAsync(int g25ContinentId, CancellationToken ct = default)
    {
        return await dbContext.G25Ethnicities
            .AsNoTracking()
            .Where(e => e.G25ContinentId == g25ContinentId)
            .OrderBy(e => e.Name)
            .Select(e => new GetG25EthnicityContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                G25ContinentId = e.G25ContinentId,
                G25ContinentName = e.G25Continent.Name,
                Regions = e.G25Regions
                    .OrderBy(r => r.Name)
                    .Select(r => new GetG25EthnicityContract.RegionSummary
                    {
                        Id = r.Id,
                        Name = r.Name
                    })
                    .ToList()
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<GetG25EthnicityAdminContract.Response>> GetAllAdminAsync(CancellationToken ct = default)
    {
        return await dbContext.G25Ethnicities
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .Select(e => new GetG25EthnicityAdminContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                G25ContinentId = e.G25ContinentId,
                G25ContinentName = e.G25Continent.Name,
                Regions = e.G25Regions
                    .OrderBy(r => r.Name)
                    .Select(r => new GetG25EthnicityContract.RegionSummary
                    {
                        Id = r.Id,
                        Name = r.Name
                    })
                    .ToList()
            })
            .ToListAsync(ct);
    }

    public async Task<GetG25EthnicityAdminContract.Response?> GetByIdAdminAsync(int id, CancellationToken ct = default)
    {
        return await dbContext.G25Ethnicities
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new GetG25EthnicityAdminContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                G25ContinentId = e.G25ContinentId,
                G25ContinentName = e.G25Continent.Name,
                Regions = e.G25Regions
                    .OrderBy(r => r.Name)
                    .Select(r => new GetG25EthnicityContract.RegionSummary
                    {
                        Id = r.Id,
                        Name = r.Name
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(GetG25EthnicityAdminContract.Response? Response, string? Error)> CreateAsync(
        CreateG25EthnicityContract.Request request, CancellationToken ct = default)
    {
        var error = await ValidateAsync(request.Name, request.G25ContinentId, null, ct);
        if (error is not null) return (null, error);

        var entity = new G25Ethnicity
        {
            Name = request.Name.Trim(),
            G25ContinentId = request.G25ContinentId,
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.G25Ethnicities.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAdminAsync(entity.Id, ct);
        return (response, null);
    }

    public async Task<(GetG25EthnicityAdminContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(
        int id, UpdateG25EthnicityContract.Request request, CancellationToken ct = default)
    {
        var entity = await dbContext.G25Ethnicities.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return (null, null, true);

        var error = await ValidateAsync(request.Name, request.G25ContinentId, id, ct);
        if (error is not null) return (null, error, false);

        entity.Name = request.Name.Trim();
        entity.G25ContinentId = request.G25ContinentId;
        entity.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAdminAsync(entity.Id, ct);
        return (response, null, false);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await dbContext.G25Ethnicities
            .Include(e => e.G25Regions)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return false;

        dbContext.G25Ethnicities.Remove(entity);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string?> ValidateAsync(string name, int continentId, int? existingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            return "Name is required and must be 1-100 characters.";

        var continentExists = await dbContext.G25Continents.AnyAsync(c => c.Id == continentId, ct);
        if (!continentExists) return "The specified G25 continent does not exist.";

        var trimmed = name.Trim();
        var exists = await dbContext.G25Ethnicities
            .AsNoTracking()
            .AnyAsync(e => e.Name == trimmed && (existingId == null || e.Id != existingId), ct);
        if (exists) return $"A G25 ethnicity named '{trimmed}' already exists.";

        return null;
    }
}
