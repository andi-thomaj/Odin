using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.ChangelogManagement.Models;

namespace Odin.Api.Endpoints.ChangelogManagement;

public interface IChangelogService
{
    Task<IReadOnlyList<GetChangelogContract.VersionResponse>> GetPublishedAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GetChangelogContract.VersionResponse>> GetAllForAdminAsync(CancellationToken cancellationToken = default);
    Task<CreateVersionContract.Response> CreateVersionAsync(string identityId, CreateVersionContract.Request request, CancellationToken cancellationToken = default);
    Task<CreateVersionContract.Response?> UpdateVersionAsync(int id, string identityId, UpdateVersionContract.Request request, CancellationToken cancellationToken = default);
    Task<bool> DeleteVersionAsync(int id, CancellationToken cancellationToken = default);
    Task<CreateEntryContract.Response?> CreateEntryAsync(int versionId, string identityId, CreateEntryContract.Request request, CancellationToken cancellationToken = default);
    Task<CreateEntryContract.Response?> UpdateEntryAsync(int id, string identityId, UpdateEntryContract.Request request, CancellationToken cancellationToken = default);
    Task<bool> DeleteEntryAsync(int id, CancellationToken cancellationToken = default);
}

public class ChangelogService(ApplicationDbContext dbContext) : IChangelogService
{
    private static readonly HashSet<string> ValidEntryTypes = ["Feature", "BugFix", "Improvement"];

    public static bool IsValidEntryType(string type) =>
        ValidEntryTypes.Contains(type);

    public async Task<IReadOnlyList<GetChangelogContract.VersionResponse>> GetPublishedAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.ChangelogVersions
            .AsNoTracking()
            .Where(v => v.IsPublished)
            .OrderByDescending(v => v.ReleasedAt)
            .Select(v => new GetChangelogContract.VersionResponse
            {
                Id = v.Id,
                Version = v.Version,
                Title = v.Title,
                ReleasedAt = v.ReleasedAt,
                IsPublished = v.IsPublished,
                Entries = v.Entries
                    .OrderBy(e => e.DisplayOrder)
                    .ThenBy(e => e.Id)
                    .Select(e => new GetChangelogContract.EntryResponse
                    {
                        Id = e.Id,
                        Type = e.Type,
                        Description = e.Description,
                        DisplayOrder = e.DisplayOrder
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GetChangelogContract.VersionResponse>> GetAllForAdminAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.ChangelogVersions
            .AsNoTracking()
            .OrderByDescending(v => v.ReleasedAt)
            .Select(v => new GetChangelogContract.VersionResponse
            {
                Id = v.Id,
                Version = v.Version,
                Title = v.Title,
                ReleasedAt = v.ReleasedAt,
                IsPublished = v.IsPublished,
                Entries = v.Entries
                    .OrderBy(e => e.DisplayOrder)
                    .ThenBy(e => e.Id)
                    .Select(e => new GetChangelogContract.EntryResponse
                    {
                        Id = e.Id,
                        Type = e.Type,
                        Description = e.Description,
                        DisplayOrder = e.DisplayOrder
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<CreateVersionContract.Response> CreateVersionAsync(string identityId, CreateVersionContract.Request request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entity = new ChangelogVersion
        {
            Version = request.Version.Trim(),
            Title = request.Title.Trim(),
            ReleasedAt = request.ReleasedAt.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(request.ReleasedAt, DateTimeKind.Utc)
                : request.ReleasedAt.ToUniversalTime(),
            IsPublished = request.IsPublished,
            CreatedAt = now,
            CreatedBy = identityId,
            UpdatedAt = now,
            UpdatedBy = identityId
        };

        dbContext.ChangelogVersions.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateVersionContract.Response
        {
            Id = entity.Id,
            Version = entity.Version,
            Title = entity.Title,
            ReleasedAt = entity.ReleasedAt,
            IsPublished = entity.IsPublished
        };
    }

    public async Task<CreateVersionContract.Response?> UpdateVersionAsync(int id, string identityId, UpdateVersionContract.Request request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ChangelogVersions.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (entity is null) return null;

        var now = DateTime.UtcNow;
        entity.Version = request.Version.Trim();
        entity.Title = request.Title.Trim();
        entity.ReleasedAt = request.ReleasedAt.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(request.ReleasedAt, DateTimeKind.Utc)
            : request.ReleasedAt.ToUniversalTime();
        entity.IsPublished = request.IsPublished;
        entity.UpdatedAt = now;
        entity.UpdatedBy = identityId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateVersionContract.Response
        {
            Id = entity.Id,
            Version = entity.Version,
            Title = entity.Title,
            ReleasedAt = entity.ReleasedAt,
            IsPublished = entity.IsPublished
        };
    }

    public async Task<bool> DeleteVersionAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ChangelogVersions.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
        if (entity is null) return false;

        dbContext.ChangelogVersions.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<CreateEntryContract.Response?> CreateEntryAsync(int versionId, string identityId, CreateEntryContract.Request request,
        CancellationToken cancellationToken = default)
    {
        var versionExists = await dbContext.ChangelogVersions.AnyAsync(v => v.Id == versionId, cancellationToken);
        if (!versionExists) return null;

        var now = DateTime.UtcNow;
        var entity = new ChangelogEntry
        {
            ChangelogVersionId = versionId,
            Type = request.Type,
            Description = request.Description.Trim(),
            DisplayOrder = request.DisplayOrder,
            CreatedAt = now,
            CreatedBy = identityId,
            UpdatedAt = now,
            UpdatedBy = identityId
        };

        dbContext.ChangelogEntries.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateEntryContract.Response
        {
            Id = entity.Id,
            ChangelogVersionId = entity.ChangelogVersionId,
            Type = entity.Type,
            Description = entity.Description,
            DisplayOrder = entity.DisplayOrder
        };
    }

    public async Task<CreateEntryContract.Response?> UpdateEntryAsync(int id, string identityId, UpdateEntryContract.Request request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ChangelogEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null) return null;

        var now = DateTime.UtcNow;
        entity.Type = request.Type;
        entity.Description = request.Description.Trim();
        entity.DisplayOrder = request.DisplayOrder;
        entity.UpdatedAt = now;
        entity.UpdatedBy = identityId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateEntryContract.Response
        {
            Id = entity.Id,
            ChangelogVersionId = entity.ChangelogVersionId,
            Type = entity.Type,
            Description = entity.Description,
            DisplayOrder = entity.DisplayOrder
        };
    }

    public async Task<bool> DeleteEntryAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.ChangelogEntries.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null) return false;

        dbContext.ChangelogEntries.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
