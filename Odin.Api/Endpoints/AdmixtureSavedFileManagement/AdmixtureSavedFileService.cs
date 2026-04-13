using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.AdmixtureSavedFileManagement.Models;

namespace Odin.Api.Endpoints.AdmixtureSavedFileManagement;

public interface IAdmixtureSavedFileService
{
    Task<IReadOnlyList<GetAdmixtureSavedFileContract.Summary>> GetAllForUserAsync(int userId,
        CancellationToken cancellationToken = default);

    Task<GetAdmixtureSavedFileContract.Response?> GetByIdForUserAsync(int id, int userId,
        CancellationToken cancellationToken = default);

    Task<GetAdmixtureSavedFileContract.Summary> CreateAsync(int userId, string identityId,
        CreateAdmixtureSavedFileContract.Request request, CancellationToken cancellationToken = default);

    Task<GetAdmixtureSavedFileContract.Summary?> UpdateAsync(int id, int userId, string identityId,
        UpdateAdmixtureSavedFileContract.Request request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default);
}

public class AdmixtureSavedFileService(ApplicationDbContext dbContext) : IAdmixtureSavedFileService
{
    private const int MaxTitleLength = 200;

    public async Task<IReadOnlyList<GetAdmixtureSavedFileContract.Summary>> GetAllForUserAsync(int userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.AdmixtureSavedFiles
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.UpdatedAt)
            .Select(e => new GetAdmixtureSavedFileContract.Summary
            {
                Id = e.Id,
                Title = e.Title,
                LineCount = e.Content.Length == 0
                    ? 0
                    : e.Content.Length - e.Content.Replace("\n", "").Length + 1,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<GetAdmixtureSavedFileContract.Response?> GetByIdForUserAsync(int id, int userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.AdmixtureSavedFiles
            .AsNoTracking()
            .Where(e => e.Id == id && e.UserId == userId)
            .Select(e => new GetAdmixtureSavedFileContract.Response
            {
                Id = e.Id,
                Title = e.Title,
                Content = e.Content,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<GetAdmixtureSavedFileContract.Summary> CreateAsync(int userId, string identityId,
        CreateAdmixtureSavedFileContract.Request request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var content = request.Content ?? string.Empty;
        var entity = new AdmixtureSavedFile
        {
            UserId = userId,
            Title = Truncate(request.Title.Trim(), MaxTitleLength),
            Content = content,
            CreatedAt = now,
            CreatedBy = identityId,
            UpdatedAt = now,
            UpdatedBy = identityId
        };

        dbContext.AdmixtureSavedFiles.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new GetAdmixtureSavedFileContract.Summary
        {
            Id = entity.Id,
            Title = entity.Title,
            LineCount = content.Length == 0 ? 0 : content.Count(c => c == '\n') + 1,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task<GetAdmixtureSavedFileContract.Summary?> UpdateAsync(int id, int userId, string identityId,
        UpdateAdmixtureSavedFileContract.Request request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AdmixtureSavedFiles
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, cancellationToken);
        if (entity is null) return null;

        entity.Title = Truncate(request.Title.Trim(), MaxTitleLength);
        entity.Content = request.Content ?? string.Empty;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = identityId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GetAdmixtureSavedFileContract.Summary
        {
            Id = entity.Id,
            Title = entity.Title,
            LineCount = entity.Content.Length == 0 ? 0 : entity.Content.Count(c => c == '\n') + 1,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task<bool> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.AdmixtureSavedFiles
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, cancellationToken);
        if (entity is null) return false;

        dbContext.AdmixtureSavedFiles.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
