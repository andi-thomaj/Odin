using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25RegionManagement.Models;

namespace Odin.Api.Endpoints.G25RegionManagement;

public interface IG25RegionService
{
    Task<IReadOnlyList<GetG25RegionContract.Response>> GetAllAsync(CancellationToken ct = default);
    Task<GetG25RegionContract.Response?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<(GetG25RegionContract.Response? Response, string? Error)> CreateAsync(CreateG25RegionContract.Request request, CancellationToken ct = default);
    Task<(GetG25RegionContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(int id, UpdateG25RegionContract.Request request, CancellationToken ct = default);
    Task<(bool Deleted, string? Error, bool NotFound)> DeleteAsync(int id, CancellationToken ct = default);
}

public class G25RegionService(ApplicationDbContext dbContext) : IG25RegionService
{
    public async Task<IReadOnlyList<GetG25RegionContract.Response>> GetAllAsync(CancellationToken ct = default)
    {
        return await dbContext.G25Regions
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .Select(e => new GetG25RegionContract.Response { Id = e.Id, Name = e.Name })
            .ToListAsync(ct);
    }

    public async Task<GetG25RegionContract.Response?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await dbContext.G25Regions
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new GetG25RegionContract.Response { Id = e.Id, Name = e.Name })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(GetG25RegionContract.Response? Response, string? Error)> CreateAsync(
        CreateG25RegionContract.Request request, CancellationToken ct = default)
    {
        var error = await ValidateNameAsync(request.Name, null, ct);
        if (error is not null) return (null, error);

        var entity = new G25Region
        {
            Name = request.Name.Trim(),
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

        var error = await ValidateNameAsync(request.Name, id, ct);
        if (error is not null) return (null, error, false);

        entity.Name = request.Name.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAsync(entity.Id, ct);
        return (response, null, false);
    }

    public async Task<(bool Deleted, string? Error, bool NotFound)> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await dbContext.G25Regions.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return (false, null, true);

        var hasEthnicities = await dbContext.G25Ethnicities.AnyAsync(e => e.G25RegionId == id, ct);
        if (hasEthnicities)
            return (false, "Cannot delete a region that still has G25 ethnicities assigned to it.", false);

        dbContext.G25Regions.Remove(entity);
        await dbContext.SaveChangesAsync(ct);
        return (true, null, false);
    }

    private async Task<string?> ValidateNameAsync(string name, int? existingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            return "Name is required and must be 1-100 characters.";

        var trimmed = name.Trim();
        var exists = await dbContext.G25Regions
            .AsNoTracking()
            .AnyAsync(e => e.Name == trimmed && (existingId == null || e.Id != existingId), ct);
        if (exists) return $"A G25 region named '{trimmed}' already exists.";

        return null;
    }
}
