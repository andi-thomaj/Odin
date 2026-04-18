using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25RegionManagement.Models;

namespace Odin.Api.Endpoints.G25RegionManagement;

public interface IG25RegionService
{
    Task<IReadOnlyList<GetG25RegionContract.Response>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<GetG25RegionContract.Response>> GetByEthnicityIdAsync(int g25EthnicityId, CancellationToken ct = default);
    Task<GetG25RegionContract.Response?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<(GetG25RegionContract.Response? Response, string? Error)> CreateAsync(CreateG25RegionContract.Request request, CancellationToken ct = default);
    Task<(GetG25RegionContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(int id, UpdateG25RegionContract.Request request, CancellationToken ct = default);
    Task<(bool Deleted, string? Error, bool NotFound)> DeleteAsync(int id, CancellationToken ct = default);
}

public class G25RegionService(ApplicationDbContext dbContext) : IG25RegionService
{
    private const int MaxRegionsPerEthnicity = 4;

    public async Task<IReadOnlyList<GetG25RegionContract.Response>> GetAllAsync(CancellationToken ct = default)
    {
        return await dbContext.G25Regions
            .AsNoTracking()
            .OrderBy(e => e.G25Ethnicity.Name)
            .ThenBy(e => e.Name)
            .Select(e => new GetG25RegionContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                G25EthnicityId = e.G25EthnicityId,
                G25EthnicityName = e.G25Ethnicity.Name,
                HasAdmixtureFile = e.AdmixtureFile != null
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<GetG25RegionContract.Response>> GetByEthnicityIdAsync(int g25EthnicityId, CancellationToken ct = default)
    {
        return await dbContext.G25Regions
            .AsNoTracking()
            .Where(e => e.G25EthnicityId == g25EthnicityId)
            .OrderBy(e => e.Name)
            .Select(e => new GetG25RegionContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                G25EthnicityId = e.G25EthnicityId,
                G25EthnicityName = e.G25Ethnicity.Name,
                HasAdmixtureFile = e.AdmixtureFile != null
            })
            .ToListAsync(ct);
    }

    public async Task<GetG25RegionContract.Response?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await dbContext.G25Regions
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new GetG25RegionContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                G25EthnicityId = e.G25EthnicityId,
                G25EthnicityName = e.G25Ethnicity.Name,
                HasAdmixtureFile = e.AdmixtureFile != null
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(GetG25RegionContract.Response? Response, string? Error)> CreateAsync(
        CreateG25RegionContract.Request request, CancellationToken ct = default)
    {
        var ethnicityExists = await dbContext.G25Ethnicities.AnyAsync(e => e.Id == request.G25EthnicityId, ct);
        if (!ethnicityExists) return (null, "The specified G25 ethnicity does not exist.");

        var error = await ValidateNameAsync(request.Name, request.G25EthnicityId, null, ct);
        if (error is not null) return (null, error);

        var currentCount = await dbContext.G25Regions.CountAsync(r => r.G25EthnicityId == request.G25EthnicityId, ct);
        if (currentCount >= MaxRegionsPerEthnicity)
            return (null, $"A G25 ethnicity can have at most {MaxRegionsPerEthnicity} regions.");

        var entity = new G25Region
        {
            Name = request.Name.Trim(),
            G25EthnicityId = request.G25EthnicityId,
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.G25Regions.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAsync(entity.Id, ct);
        return (response, null);
    }

    public async Task<(GetG25RegionContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(
        int id, UpdateG25RegionContract.Request request, CancellationToken ct = default)
    {
        var entity = await dbContext.G25Regions.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return (null, null, true);

        var ethnicityExists = await dbContext.G25Ethnicities.AnyAsync(e => e.Id == request.G25EthnicityId, ct);
        if (!ethnicityExists) return (null, "The specified G25 ethnicity does not exist.", false);

        var error = await ValidateNameAsync(request.Name, request.G25EthnicityId, id, ct);
        if (error is not null) return (null, error, false);

        if (entity.G25EthnicityId != request.G25EthnicityId)
        {
            var targetCount = await dbContext.G25Regions.CountAsync(
                r => r.G25EthnicityId == request.G25EthnicityId && r.Id != id, ct);
            if (targetCount >= MaxRegionsPerEthnicity)
                return (null, $"A G25 ethnicity can have at most {MaxRegionsPerEthnicity} regions.", false);
        }

        entity.Name = request.Name.Trim();
        entity.G25EthnicityId = request.G25EthnicityId;
        entity.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAsync(entity.Id, ct);
        return (response, null, false);
    }

    public async Task<(bool Deleted, string? Error, bool NotFound)> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await dbContext.G25Regions
            .Include(r => r.AdmixtureFile)
            .FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return (false, null, true);

        dbContext.G25Regions.Remove(entity);
        await dbContext.SaveChangesAsync(ct);
        return (true, null, false);
    }

    private async Task<string?> ValidateNameAsync(string name, int ethnicityId, int? existingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            return "Name is required and must be 1-100 characters.";

        var trimmed = name.Trim();
        var exists = await dbContext.G25Regions
            .AsNoTracking()
            .AnyAsync(e => e.G25EthnicityId == ethnicityId && e.Name == trimmed && (existingId == null || e.Id != existingId), ct);
        if (exists) return $"A G25 region named '{trimmed}' already exists for this ethnicity.";

        return null;
    }
}
