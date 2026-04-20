using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25DistancePopulationSampleManagement.Models;

namespace Odin.Api.Endpoints.G25DistancePopulationSampleManagement;

public interface IG25DistancePopulationSampleService
{
    Task<IReadOnlyList<GetG25DistancePopulationSampleContract.Response>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<GetG25DistancePopulationSampleContract.PagedResponse> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<GetG25DistancePopulationSampleContract.Response?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CreateG25DistancePopulationSampleContract.Response> CreateAsync(string identityId, CreateG25DistancePopulationSampleContract.Request request,
        CancellationToken cancellationToken = default);
    Task<GetG25DistancePopulationSampleContract.Response?> UpdateAsync(int id, string identityId, UpdateG25DistancePopulationSampleContract.Request request,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> SearchLabelsAsync(string query, int limit = 50, CancellationToken cancellationToken = default);
}

public class G25DistancePopulationSampleService(ApplicationDbContext dbContext) : IG25DistancePopulationSampleService
{
    private const int MaxPageSize = 200;
    private const int MaxSearchLimit = 200;

    public async Task<IReadOnlyList<GetG25DistancePopulationSampleContract.Response>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.G25DistancePopulationSamples
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .Select(e => new GetG25DistancePopulationSampleContract.Response
            {
                Id = e.Id,
                Label = e.Label,
                Coordinates = e.Coordinates,
                Ids = e.Ids,
                G25DistanceEraId = e.G25DistanceEraId,
                G25DistanceEra = e.G25DistanceEra == null
                    ? null
                    : new G25DistanceEraSummaryDto
                    {
                        Id = e.G25DistanceEra.Id,
                        Name = e.G25DistanceEra.Name
                    },
                ResearchLinks = e.ResearchLinks
                    .OrderBy(r => r.Id)
                    .Select(r => new ResearchLinkDto.Response
                    {
                        Id = r.Id,
                        Label = r.Label,
                        Link = r.Link
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<GetG25DistancePopulationSampleContract.PagedResponse> GetPagedAsync(int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var query = dbContext.G25DistancePopulationSamples.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new GetG25DistancePopulationSampleContract.Response
            {
                Id = e.Id,
                Label = e.Label,
                Coordinates = e.Coordinates,
                Ids = e.Ids,
                G25DistanceEraId = e.G25DistanceEraId,
                G25DistanceEra = e.G25DistanceEra == null
                    ? null
                    : new G25DistanceEraSummaryDto
                    {
                        Id = e.G25DistanceEra.Id,
                        Name = e.G25DistanceEra.Name
                    },
                ResearchLinks = e.ResearchLinks
                    .OrderBy(r => r.Id)
                    .Select(r => new ResearchLinkDto.Response
                    {
                        Id = r.Id,
                        Label = r.Label,
                        Link = r.Link
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return new GetG25DistancePopulationSampleContract.PagedResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<GetG25DistancePopulationSampleContract.Response?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.G25DistancePopulationSamples
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new GetG25DistancePopulationSampleContract.Response
            {
                Id = e.Id,
                Label = e.Label,
                Coordinates = e.Coordinates,
                Ids = e.Ids,
                G25DistanceEraId = e.G25DistanceEraId,
                G25DistanceEra = e.G25DistanceEra == null
                    ? null
                    : new G25DistanceEraSummaryDto
                    {
                        Id = e.G25DistanceEra.Id,
                        Name = e.G25DistanceEra.Name
                    },
                ResearchLinks = e.ResearchLinks
                    .OrderBy(r => r.Id)
                    .Select(r => new ResearchLinkDto.Response
                    {
                        Id = r.Id,
                        Label = r.Label,
                        Link = r.Link
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> SearchLabelsAsync(string query, int limit = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<string>();

        if (limit < 1) limit = 50;
        if (limit > MaxSearchLimit) limit = MaxSearchLimit;

        var normalized = query.Trim();
        var lower = normalized.ToLowerInvariant();

        return await dbContext.G25DistancePopulationSamples
            .AsNoTracking()
            .Where(e => e.Label.ToLower().Contains(lower))
            .OrderBy(e => e.Id)
            .Take(limit)
            .Select(e => e.Label)
            .ToListAsync(cancellationToken);
    }

    public async Task<CreateG25DistancePopulationSampleContract.Response> CreateAsync(string identityId, CreateG25DistancePopulationSampleContract.Request request,
        CancellationToken cancellationToken = default)
    {
        G25DistanceEra? era = null;
        if (request.G25DistanceEraId is int eraId)
        {
            era = await dbContext.G25DistanceEras
                .FirstOrDefaultAsync(e => e.Id == eraId, cancellationToken)
                ?? throw new ArgumentException($"G25 distance era {eraId} not found.", nameof(request));
        }

        var now = DateTime.UtcNow;
        var entity = new G25DistancePopulationSample
        {
            Label = request.Label.Trim(),
            Coordinates = request.Coordinates.Trim(),
            Ids = request.Ids?.Trim() ?? string.Empty,
            G25DistanceEraId = era?.Id,
            CreatedAt = now,
            CreatedBy = identityId,
            UpdatedAt = now,
            UpdatedBy = identityId
        };

        if (request.ResearchLinks is { Count: > 0 })
        {
            foreach (var link in request.ResearchLinks)
            {
                var label = link.Label?.Trim();
                var url = link.Link?.Trim();
                if (string.IsNullOrWhiteSpace(label) || string.IsNullOrWhiteSpace(url)) continue;
                entity.ResearchLinks.Add(new ResearchLink
                {
                    Label = label,
                    Link = url,
                    CreatedAt = now,
                    CreatedBy = identityId,
                    UpdatedAt = now,
                    UpdatedBy = identityId
                });
            }
        }

        dbContext.G25DistancePopulationSamples.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateG25DistancePopulationSampleContract.Response
        {
            Id = entity.Id,
            Label = entity.Label,
            Coordinates = entity.Coordinates,
            Ids = entity.Ids,
            G25DistanceEraId = entity.G25DistanceEraId,
            G25DistanceEra = era is null
                ? null
                : new G25DistanceEraSummaryDto { Id = era.Id, Name = era.Name },
            ResearchLinks = entity.ResearchLinks
                .OrderBy(r => r.Id)
                .Select(r => new ResearchLinkDto.Response
                {
                    Id = r.Id,
                    Label = r.Label,
                    Link = r.Link
                })
                .ToList()
        };
    }

    public async Task<GetG25DistancePopulationSampleContract.Response?> UpdateAsync(int id, string identityId, UpdateG25DistancePopulationSampleContract.Request request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.G25DistancePopulationSamples
            .Include(e => e.G25DistanceEra)
            .Include(e => e.ResearchLinks)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null) return null;

        G25DistanceEra? era = null;
        if (request.G25DistanceEraId is int eraId)
        {
            era = await dbContext.G25DistanceEras
                .FirstOrDefaultAsync(e => e.Id == eraId, cancellationToken)
                ?? throw new ArgumentException($"G25 distance era {eraId} not found.", nameof(request));
        }

        var now = DateTime.UtcNow;
        entity.Label = request.Label.Trim();
        entity.Coordinates = request.Coordinates.Trim();
        entity.Ids = request.Ids?.Trim() ?? string.Empty;
        entity.G25DistanceEraId = era?.Id;
        entity.UpdatedAt = now;
        entity.UpdatedBy = identityId;

        if (request.ResearchLinks is not null)
        {
            var incoming = request.ResearchLinks
                .Select(l => new
                {
                    l.Id,
                    Label = l.Label?.Trim() ?? string.Empty,
                    Link = l.Link?.Trim() ?? string.Empty
                })
                .Where(l => !string.IsNullOrWhiteSpace(l.Label) && !string.IsNullOrWhiteSpace(l.Link))
                .ToList();

            var incomingIds = incoming.Where(l => l.Id.HasValue).Select(l => l.Id!.Value).ToHashSet();
            var toRemove = entity.ResearchLinks.Where(r => !incomingIds.Contains(r.Id)).ToList();
            foreach (var link in toRemove)
            {
                entity.ResearchLinks.Remove(link);
                dbContext.ResearchLinks.Remove(link);
            }

            foreach (var item in incoming)
            {
                if (item.Id is int existingId)
                {
                    var existing = entity.ResearchLinks.FirstOrDefault(r => r.Id == existingId);
                    if (existing is null) continue;
                    existing.Label = item.Label;
                    existing.Link = item.Link;
                    existing.UpdatedAt = now;
                    existing.UpdatedBy = identityId;
                }
                else
                {
                    entity.ResearchLinks.Add(new ResearchLink
                    {
                        Label = item.Label,
                        Link = item.Link,
                        CreatedAt = now,
                        CreatedBy = identityId,
                        UpdatedAt = now,
                        UpdatedBy = identityId
                    });
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GetG25DistancePopulationSampleContract.Response
        {
            Id = entity.Id,
            Label = entity.Label,
            Coordinates = entity.Coordinates,
            Ids = entity.Ids,
            G25DistanceEraId = entity.G25DistanceEraId,
            G25DistanceEra = era is null
                ? null
                : new G25DistanceEraSummaryDto { Id = era.Id, Name = era.Name },
            ResearchLinks = entity.ResearchLinks
                .OrderBy(r => r.Id)
                .Select(r => new ResearchLinkDto.Response
                {
                    Id = r.Id,
                    Label = r.Label,
                    Link = r.Link
                })
                .ToList()
        };
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.G25DistancePopulationSamples.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null) return false;

        dbContext.G25DistancePopulationSamples.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
