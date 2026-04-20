using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25DistanceEraManagement.Models;

namespace Odin.Api.Endpoints.G25DistanceEraManagement;

public interface IG25DistanceEraService
{
    Task<IReadOnlyList<GetG25DistanceEraContract.Response>> GetAllAsync(CancellationToken ct = default);
    Task<GetG25DistanceEraContract.Response?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<(GetG25DistanceEraContract.Response? Response, string? Error)> CreateAsync(CreateG25DistanceEraContract.Request request, CancellationToken ct = default);
    Task<(GetG25DistanceEraContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(int id, UpdateG25DistanceEraContract.Request request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class G25DistanceEraService(ApplicationDbContext dbContext) : IG25DistanceEraService
{
    public async Task<IReadOnlyList<GetG25DistanceEraContract.Response>> GetAllAsync(CancellationToken ct = default)
    {
        return await dbContext.G25DistanceEras
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .Select(e => new GetG25DistanceEraContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                SampleCount = e.G25DistancePopulationSamples.Count
            })
            .ToListAsync(ct);
    }

    public async Task<GetG25DistanceEraContract.Response?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await dbContext.G25DistanceEras
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new GetG25DistanceEraContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                SampleCount = e.G25DistancePopulationSamples.Count
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(GetG25DistanceEraContract.Response? Response, string? Error)> CreateAsync(
        CreateG25DistanceEraContract.Request request, CancellationToken ct = default)
    {
        var error = await ValidateNameAsync(request.Name, null, ct);
        if (error is not null) return (null, error);

        var entity = new G25DistanceEra
        {
            Name = request.Name.Trim(),
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.G25DistanceEras.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAsync(entity.Id, ct);
        return (response, null);
    }

    public async Task<(GetG25DistanceEraContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(
        int id, UpdateG25DistanceEraContract.Request request, CancellationToken ct = default)
    {
        var entity = await dbContext.G25DistanceEras.FirstOrDefaultAsync(e => e.Id == id, ct);
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
        var entity = await dbContext.G25DistanceEras.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return false;

        dbContext.G25DistanceEras.Remove(entity);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string?> ValidateNameAsync(string name, int? existingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            return "Name is required and must be 1-100 characters.";

        var trimmed = name.Trim();
        var exists = await dbContext.G25DistanceEras
            .AsNoTracking()
            .AnyAsync(e => e.Name == trimmed && (existingId == null || e.Id != existingId), ct);
        if (exists) return $"A G25 distance era named '{trimmed}' already exists.";

        return null;
    }
}
