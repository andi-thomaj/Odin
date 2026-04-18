using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25ContinentManagement.Models;

namespace Odin.Api.Endpoints.G25ContinentManagement;

public interface IG25ContinentService
{
    Task<IReadOnlyList<GetG25ContinentContract.Response>> GetAllAsync(CancellationToken ct = default);
    Task<GetG25ContinentContract.Response?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<(GetG25ContinentContract.Response? Response, string? Error)> CreateAsync(CreateG25ContinentContract.Request request, CancellationToken ct = default);
    Task<(GetG25ContinentContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(int id, UpdateG25ContinentContract.Request request, CancellationToken ct = default);
    Task<(bool Deleted, string? Error)> DeleteAsync(int id, CancellationToken ct = default);
}

public class G25ContinentService(ApplicationDbContext dbContext) : IG25ContinentService
{
    public async Task<IReadOnlyList<GetG25ContinentContract.Response>> GetAllAsync(CancellationToken ct = default)
    {
        return await dbContext.G25Continents
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new GetG25ContinentContract.Response
            {
                Id = c.Id,
                Name = c.Name,
                EthnicityCount = c.G25Ethnicities.Count
            })
            .ToListAsync(ct);
    }

    public async Task<GetG25ContinentContract.Response?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await dbContext.G25Continents
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new GetG25ContinentContract.Response
            {
                Id = c.Id,
                Name = c.Name,
                EthnicityCount = c.G25Ethnicities.Count
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(GetG25ContinentContract.Response? Response, string? Error)> CreateAsync(
        CreateG25ContinentContract.Request request, CancellationToken ct = default)
    {
        var error = await ValidateNameAsync(request.Name, null, ct);
        if (error is not null) return (null, error);

        var entity = new G25Continent
        {
            Name = request.Name.Trim(),
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.G25Continents.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAsync(entity.Id, ct);
        return (response, null);
    }

    public async Task<(GetG25ContinentContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(
        int id, UpdateG25ContinentContract.Request request, CancellationToken ct = default)
    {
        var entity = await dbContext.G25Continents.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return (null, null, true);

        var error = await ValidateNameAsync(request.Name, id, ct);
        if (error is not null) return (null, error, false);

        entity.Name = request.Name.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAsync(entity.Id, ct);
        return (response, null, false);
    }

    public async Task<(bool Deleted, string? Error)> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await dbContext.G25Continents
            .Include(c => c.G25Ethnicities)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        if (entity is null) return (false, null);

        if (entity.G25Ethnicities.Count > 0)
            return (false, "Cannot delete a G25 continent that has ethnicities assigned.");

        dbContext.G25Continents.Remove(entity);
        await dbContext.SaveChangesAsync(ct);
        return (true, null);
    }

    private async Task<string?> ValidateNameAsync(string name, int? existingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            return "Name is required and must be 1-100 characters.";

        var trimmed = name.Trim();
        var exists = await dbContext.G25Continents
            .AsNoTracking()
            .AnyAsync(c => c.Name == trimmed && (existingId == null || c.Id != existingId), ct);
        if (exists) return $"A G25 continent named '{trimmed}' already exists.";

        return null;
    }
}
