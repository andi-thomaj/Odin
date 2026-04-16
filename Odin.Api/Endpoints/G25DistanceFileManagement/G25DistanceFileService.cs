using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25DistanceFileManagement.Models;

namespace Odin.Api.Endpoints.G25DistanceFileManagement;

public interface IG25DistanceFileService
{
    Task<IReadOnlyList<GetG25DistanceFileContract.ListItem>> GetAllAsync(CancellationToken ct = default);
    Task<GetG25DistanceFileContract.Response?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<(GetG25DistanceFileContract.Response? Response, string? Error)> CreateAsync(CreateG25DistanceFileContract.Request request, CancellationToken ct = default);
    Task<(GetG25DistanceFileContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(int id, UpdateG25DistanceFileContract.Request request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class G25DistanceFileService(ApplicationDbContext dbContext) : IG25DistanceFileService
{
    public async Task<IReadOnlyList<GetG25DistanceFileContract.ListItem>> GetAllAsync(CancellationToken ct = default)
    {
        return await dbContext.G25DistanceFiles
            .AsNoTracking()
            .OrderBy(e => e.Title)
            .Select(e => new GetG25DistanceFileContract.ListItem { Id = e.Id, Title = e.Title })
            .ToListAsync(ct);
    }

    public async Task<GetG25DistanceFileContract.Response?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await dbContext.G25DistanceFiles
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new GetG25DistanceFileContract.Response { Id = e.Id, Title = e.Title, Content = e.Content })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(GetG25DistanceFileContract.Response? Response, string? Error)> CreateAsync(
        CreateG25DistanceFileContract.Request request, CancellationToken ct = default)
    {
        var error = await ValidateTitleAsync(request.Title, null, ct);
        if (error is not null) return (null, error);
        if (string.IsNullOrWhiteSpace(request.Content)) return (null, "Content is required.");

        var entity = new G25DistanceFile
        {
            Title = request.Title.Trim(),
            Content = request.Content,
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.G25DistanceFiles.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAsync(entity.Id, ct);
        return (response, null);
    }

    public async Task<(GetG25DistanceFileContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(
        int id, UpdateG25DistanceFileContract.Request request, CancellationToken ct = default)
    {
        var entity = await dbContext.G25DistanceFiles.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return (null, null, true);

        var error = await ValidateTitleAsync(request.Title, id, ct);
        if (error is not null) return (null, error, false);
        if (string.IsNullOrWhiteSpace(request.Content)) return (null, "Content is required.", false);

        entity.Title = request.Title.Trim();
        entity.Content = request.Content;
        entity.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAsync(entity.Id, ct);
        return (response, null, false);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await dbContext.G25DistanceFiles.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return false;

        dbContext.G25DistanceFiles.Remove(entity);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }

    private async Task<string?> ValidateTitleAsync(string title, int? existingId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            return "Title is required and must be 1-200 characters.";

        var trimmed = title.Trim();
        var exists = await dbContext.G25DistanceFiles
            .AsNoTracking()
            .AnyAsync(e => e.Title == trimmed && (existingId == null || e.Id != existingId), ct);
        if (exists) return $"A distance file titled '{trimmed}' already exists.";

        return null;
    }
}
