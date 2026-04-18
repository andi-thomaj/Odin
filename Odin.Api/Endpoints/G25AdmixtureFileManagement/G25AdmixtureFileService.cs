using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25AdmixtureFileManagement.Models;

namespace Odin.Api.Endpoints.G25AdmixtureFileManagement;

public interface IG25AdmixtureFileService
{
    Task<IReadOnlyList<GetG25AdmixtureFileContract.ListItem>> GetAllAsync(CancellationToken ct = default);
    Task<GetG25AdmixtureFileContract.Response?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<GetG25AdmixtureFileContract.Response?> GetByRegionIdAsync(int g25RegionId, CancellationToken ct = default);
    Task<(GetG25AdmixtureFileContract.Response? Response, string? Error)> CreateAsync(CreateG25AdmixtureFileContract.Request request, CancellationToken ct = default);
    Task<(GetG25AdmixtureFileContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(int id, UpdateG25AdmixtureFileContract.Request request, CancellationToken ct = default);
    Task<bool> DeleteAsync(int id, CancellationToken ct = default);
}

public class G25AdmixtureFileService(ApplicationDbContext dbContext) : IG25AdmixtureFileService
{
    public async Task<IReadOnlyList<GetG25AdmixtureFileContract.ListItem>> GetAllAsync(CancellationToken ct = default)
    {
        return await dbContext.G25AdmixtureFiles
            .AsNoTracking()
            .OrderBy(e => e.G25Region.G25Ethnicity.Name)
            .ThenBy(e => e.G25Region.Name)
            .Select(e => new GetG25AdmixtureFileContract.ListItem
            {
                Id = e.Id,
                Name = e.Name,
                G25RegionId = e.G25RegionId,
                G25RegionName = e.G25Region.Name,
                G25EthnicityId = e.G25Region.G25EthnicityId,
                G25EthnicityName = e.G25Region.G25Ethnicity.Name
            })
            .ToListAsync(ct);
    }

    public async Task<GetG25AdmixtureFileContract.Response?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await dbContext.G25AdmixtureFiles
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new GetG25AdmixtureFileContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                Content = e.Content,
                G25RegionId = e.G25RegionId,
                G25RegionName = e.G25Region.Name,
                G25EthnicityId = e.G25Region.G25EthnicityId,
                G25EthnicityName = e.G25Region.G25Ethnicity.Name
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<GetG25AdmixtureFileContract.Response?> GetByRegionIdAsync(int g25RegionId, CancellationToken ct = default)
    {
        return await dbContext.G25AdmixtureFiles
            .AsNoTracking()
            .Where(e => e.G25RegionId == g25RegionId)
            .Select(e => new GetG25AdmixtureFileContract.Response
            {
                Id = e.Id,
                Name = e.Name,
                Content = e.Content,
                G25RegionId = e.G25RegionId,
                G25RegionName = e.G25Region.Name,
                G25EthnicityId = e.G25Region.G25EthnicityId,
                G25EthnicityName = e.G25Region.G25Ethnicity.Name
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<(GetG25AdmixtureFileContract.Response? Response, string? Error)> CreateAsync(
        CreateG25AdmixtureFileContract.Request request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            return (null, "Name is required and must be 1-200 characters.");
        if (string.IsNullOrWhiteSpace(request.Content))
            return (null, "Content is required.");

        var regionExists = await dbContext.G25Regions.AnyAsync(r => r.Id == request.G25RegionId, ct);
        if (!regionExists) return (null, "The specified G25 region does not exist.");

        var alreadyLinked = await dbContext.G25AdmixtureFiles.AnyAsync(e => e.G25RegionId == request.G25RegionId, ct);
        if (alreadyLinked) return (null, "That G25 region already has an admixture file.");

        var entity = new G25AdmixtureFile
        {
            Name = request.Name.Trim(),
            Content = request.Content,
            G25RegionId = request.G25RegionId,
            CreatedBy = "system",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        dbContext.G25AdmixtureFiles.Add(entity);
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAsync(entity.Id, ct);
        return (response, null);
    }

    public async Task<(GetG25AdmixtureFileContract.Response? Response, string? Error, bool NotFound)> UpdateAsync(
        int id, UpdateG25AdmixtureFileContract.Request request, CancellationToken ct = default)
    {
        var entity = await dbContext.G25AdmixtureFiles.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return (null, null, true);

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            return (null, "Name is required and must be 1-200 characters.", false);
        if (string.IsNullOrWhiteSpace(request.Content))
            return (null, "Content is required.", false);

        var regionExists = await dbContext.G25Regions.AnyAsync(r => r.Id == request.G25RegionId, ct);
        if (!regionExists) return (null, "The specified G25 region does not exist.", false);

        var alreadyLinked = await dbContext.G25AdmixtureFiles.AnyAsync(
            e => e.G25RegionId == request.G25RegionId && e.Id != id, ct);
        if (alreadyLinked) return (null, "That G25 region already has an admixture file.", false);

        entity.Name = request.Name.Trim();
        entity.Content = request.Content;
        entity.G25RegionId = request.G25RegionId;
        entity.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(ct);

        var response = await GetByIdAsync(entity.Id, ct);
        return (response, null, false);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await dbContext.G25AdmixtureFiles.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (entity is null) return false;

        dbContext.G25AdmixtureFiles.Remove(entity);
        await dbContext.SaveChangesAsync(ct);
        return true;
    }
}
