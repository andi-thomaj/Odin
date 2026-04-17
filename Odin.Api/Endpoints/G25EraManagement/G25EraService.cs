using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25EraManagement.Models;

namespace Odin.Api.Endpoints.G25EraManagement;

public interface IG25EraService
{
    Task<IReadOnlyList<GetG25EraContract.Response>> GetAllAsync(CancellationToken ct = default);
    Task<GetG25EraContract.Response?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<(GetG25EraContract.Response? Response, string? Error)> CreateAsync(CreateG25EraContract.Request request, CancellationToken ct = default);
    Task<(GetG25EraContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(int id, UpdateG25EraContract.Request request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class G25EraService(ApplicationDbContext dbContext) : IG25EraService
{
    public async Task<IReadOnlyList<GetG25EraContract.Response>> GetAllAsync(CancellationToken ct = default)
    {
        return await dbContext.G25Eras
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .Select(e => new GetG25EraContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                DistanceFiles = e.DistanceFiles
                    .OrderBy(f => f.Title)
                    .Select(f => new GetG25EraContract.DistanceFileSummary { Id = f.Id, Title = f.Title })
                    .ToList()
            })
            .ToListAsync(ct);
    }

    public async Task<GetG25EraContract.Response?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await dbContext.G25Eras
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new GetG25EraContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                DistanceFiles = e.DistanceFiles
                    .OrderBy(f => f.Title)
                    .Select(f => new GetG25EraContract.DistanceFileSummary { Id = f.Id, Title = f.Title })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(GetG25EraContract.Response? Response, string? Error)> CreateAsync(
        CreateG25EraContract.Request request, CancellationToken ct = default)
    {
        var error = await ValidateNameAsync(request.Name, null, ct);
        if (error is not null) return (null, error);

        var entity = new G25Era
        {
            Name = request.Name.Trim(),
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.G25Eras.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAsync(entity.Id, ct);
        return (response, null);
    }

    public async Task<(GetG25EraContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(
        int id, UpdateG25EraContract.Request request, CancellationToken ct = default)
    {
        var entity = await dbContext.G25Eras.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return (null, null, true);

        var error = await ValidateNameAsync(request.Name, id, ct);
        if (error is not null) return (null, error, false);

        entity.Name = request.Name.Trim();
        entity.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAsync(entity.Id, ct);
        return (response, null, false);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await dbContext.G25Eras.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return false;

        dbContext.G25Eras.Remove(entity);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string?> ValidateNameAsync(string name, int? existingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            return "Name is required and must be 1-100 characters.";

        var trimmed = name.Trim();
        var exists = await dbContext.G25Eras
            .AsNoTracking()
            .AnyAsync(e => e.Name == trimmed && (existingId == null || e.Id != existingId), ct);
        if (exists) return $"A G25 era named '{trimmed}' already exists.";

        return null;
    }
}
