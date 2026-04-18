using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25PcaFileManagement.Models;

namespace Odin.Api.Endpoints.G25PcaFileManagement;

public interface IG25PcaFileService
{
    Task<IReadOnlyList<GetG25PcaFileContract.ListItem>> GetAllAsync(CancellationToken ct = default);
    Task<GetG25PcaFileContract.Response?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<GetG25PcaFileContract.Response?> GetByEraIdAsync(int g25EraId, CancellationToken ct = default);
    Task<(GetG25PcaFileContract.Response? Response, string? Error)> CreateAsync(CreateG25PcaFileContract.Request request, CancellationToken ct = default);
    Task<(GetG25PcaFileContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(int id, UpdateG25PcaFileContract.Request request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
    Task<(GetG25PcaFilesByContinentsContract.Response? Response, string? Error, bool NotFound)> GetByContinentIdsAsync(IReadOnlyList<int> continentIds, CancellationToken ct = default);
}

public class G25PcaFileService(ApplicationDbContext dbContext) : IG25PcaFileService
{
    public async Task<IReadOnlyList<GetG25PcaFileContract.ListItem>> GetAllAsync(CancellationToken ct = default)
    {
        return await dbContext.G25PcaFiles
            .AsNoTracking()
            .OrderBy(e => e.Era.Name)
            .Select(e => new GetG25PcaFileContract.ListItem
            {
                Id = e.Id,
                Title = e.Title,
                G25EraId = e.G25EraId,
                G25EraName = e.Era.Name
            })
            .ToListAsync(ct);
    }

    public async Task<GetG25PcaFileContract.Response?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await dbContext.G25PcaFiles
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new GetG25PcaFileContract.Response
            {
                Id = e.Id,
                Title = e.Title,
                Content = e.Content,
                G25EraId = e.G25EraId,
                G25EraName = e.Era.Name
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<GetG25PcaFileContract.Response?> GetByEraIdAsync(int g25EraId, CancellationToken ct = default)
    {
        return await dbContext.G25PcaFiles
            .AsNoTracking()
            .Where(e => e.G25EraId == g25EraId)
            .Select(e => new GetG25PcaFileContract.Response
            {
                Id = e.Id,
                Title = e.Title,
                Content = e.Content,
                G25EraId = e.G25EraId,
                G25EraName = e.Era.Name
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(GetG25PcaFileContract.Response? Response, string? Error)> CreateAsync(
        CreateG25PcaFileContract.Request request, CancellationToken ct = default)
    {
        var error = await ValidateAsync(request.Title, request.Content, request.G25EraId, null, ct);
        if (error is not null) return (null, error);

        var entity = new G25PcaFile
        {
            Title = request.Title.Trim(),
            Content = request.Content,
            G25EraId = request.G25EraId,
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.G25PcaFiles.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAsync(entity.Id, ct);
        return (response, null);
    }

    public async Task<(GetG25PcaFileContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(
        int id, UpdateG25PcaFileContract.Request request, CancellationToken ct = default)
    {
        var entity = await dbContext.G25PcaFiles.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return (null, null, true);

        var error = await ValidateAsync(request.Title, request.Content, request.G25EraId, id, ct);
        if (error is not null) return (null, error, false);

        entity.Title = request.Title.Trim();
        entity.Content = request.Content;
        entity.G25EraId = request.G25EraId;
        entity.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAsync(entity.Id, ct);
        return (response, null, false);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await dbContext.G25PcaFiles.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return false;

        dbContext.G25PcaFiles.Remove(entity);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    private const int MaxContinentsPerLookup = 8;

    public async Task<(GetG25PcaFilesByContinentsContract.Response? Response, string? Error, bool NotFound)> GetByContinentIdsAsync(
        IReadOnlyList<int> continentIds, CancellationToken ct = default)
    {
        if (continentIds is null || continentIds.Count == 0)
        {
            return (null, "At least one continent id is required.", false);
        }

        var distinctIds = continentIds.Distinct().ToList();
        if (distinctIds.Count > MaxContinentsPerLookup)
        {
            return (null, $"At most {MaxContinentsPerLookup} continents can be requested.", false);
        }

        var continents = await dbContext.G25Continents
            .AsNoTracking()
            .Where(c => distinctIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Name })
            .ToListAsync(ct);

        if (continents.Count != distinctIds.Count)
        {
            var found = continents.Select(c => c.Id).ToHashSet();
            var missing = distinctIds.Where(id => !found.Contains(id)).ToList();
            return (null, $"No continent(s) found for id(s) [{string.Join(", ", missing)}].", true);
        }

        var pcaFiles = await dbContext.G25PcaFiles
            .AsNoTracking()
            .OrderBy(f => f.Era.Name)
            .Select(f => new GetG25PcaFilesByContinentsContract.PcaFileEntry
            {
                Id = f.Id,
                Title = f.Title,
                G25EraId = f.G25EraId,
                G25EraName = f.Era.Name,
                Content = f.Content
            })
            .ToListAsync(ct);

        var bundles = distinctIds
            .Select(id =>
            {
                var continent = continents.First(c => c.Id == id);
                return new GetG25PcaFilesByContinentsContract.ContinentBundle
                {
                    G25ContinentId = continent.Id,
                    G25ContinentName = continent.Name,
                    PcaFiles = pcaFiles
                };
            })
            .ToList();

        return (new GetG25PcaFilesByContinentsContract.Response { Continents = bundles }, null, false);
    }

    private async Task<string?> ValidateAsync(string title, string content, int eraId, int? existingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            return "Title is required and must be 1-200 characters.";
        if (string.IsNullOrWhiteSpace(content))
            return "Content is required.";

        var eraExists = await dbContext.G25Eras.AnyAsync(e => e.Id == eraId, ct);
        if (!eraExists) return "The specified G25 era does not exist.";

        var trimmed = title.Trim();
        var titleExists = await dbContext.G25PcaFiles
            .AsNoTracking()
            .AnyAsync(e => e.Title == trimmed && (existingId == null || e.Id != existingId), ct);
        if (titleExists) return $"A G25 PCA file with title '{trimmed}' already exists.";

        var alreadyLinked = await dbContext.G25PcaFiles
            .AsNoTracking()
            .AnyAsync(e => e.G25EraId == eraId && (existingId == null || e.Id != existingId), ct);
        if (alreadyLinked) return "That G25 era already has a PCA file.";

        return null;
    }
}
