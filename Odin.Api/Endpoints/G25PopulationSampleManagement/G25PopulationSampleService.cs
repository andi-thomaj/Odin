using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25PopulationSampleManagement.Models;

namespace Odin.Api.Endpoints.G25PopulationSampleManagement;

public interface IG25PopulationSampleService
{
    Task<IReadOnlyList<GetG25PopulationSampleContract.Response>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<GetG25PopulationSampleContract.PagedResponse> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<GetG25PopulationSampleContract.Response?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CreateG25PopulationSampleContract.Response> CreateAsync(string identityId, CreateG25PopulationSampleContract.Request request,
        CancellationToken cancellationToken = default);
    Task<GetG25PopulationSampleContract.Response?> UpdateAsync(int id, string identityId, UpdateG25PopulationSampleContract.Request request,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> SearchLabelsAsync(string query, int limit = 50, CancellationToken cancellationToken = default);
}

public class G25PopulationSampleService(ApplicationDbContext dbContext) : IG25PopulationSampleService
{
    private const int MaxPageSize = 200;
    private const int MaxSearchLimit = 200;

    public async Task<IReadOnlyList<GetG25PopulationSampleContract.Response>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.G25PopulationSamples
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .Select(e => new GetG25PopulationSampleContract.Response
            {
                Id = e.Id,
                Label = e.Label,
                Coordinates = e.Coordinates,
                G25AdmixtureEraId = e.G25AdmixtureEraId,
                G25AdmixtureEra = e.G25AdmixtureEra == null
                    ? null
                    : new G25AdmixtureEraSummaryDto
                    {
                        Id = e.G25AdmixtureEra.Id,
                        Name = e.G25AdmixtureEra.Name
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

    public async Task<GetG25PopulationSampleContract.PagedResponse> GetPagedAsync(int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var query = dbContext.G25PopulationSamples.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new GetG25PopulationSampleContract.Response
            {
                Id = e.Id,
                Label = e.Label,
                Coordinates = e.Coordinates,
                G25AdmixtureEraId = e.G25AdmixtureEraId,
                G25AdmixtureEra = e.G25AdmixtureEra == null
                    ? null
                    : new G25AdmixtureEraSummaryDto
                    {
                        Id = e.G25AdmixtureEra.Id,
                        Name = e.G25AdmixtureEra.Name
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

        return new GetG25PopulationSampleContract.PagedResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<GetG25PopulationSampleContract.Response?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.G25PopulationSamples
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new GetG25PopulationSampleContract.Response
            {
                Id = e.Id,
                Label = e.Label,
                Coordinates = e.Coordinates,
                G25AdmixtureEraId = e.G25AdmixtureEraId,
                G25AdmixtureEra = e.G25AdmixtureEra == null
                    ? null
                    : new G25AdmixtureEraSummaryDto
                    {
                        Id = e.G25AdmixtureEra.Id,
                        Name = e.G25AdmixtureEra.Name
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

        return await dbContext.G25PopulationSamples
            .AsNoTracking()
            .Where(e => e.Label.ToLower().Contains(lower))
            .OrderBy(e => e.Id)
            .Take(limit)
            .Select(e => e.Label)
            .ToListAsync(cancellationToken);
    }

    public async Task<CreateG25PopulationSampleContract.Response> CreateAsync(string identityId, CreateG25PopulationSampleContract.Request request,
        CancellationToken cancellationToken = default)
    {
        G25AdmixtureEra? era = null;
        if (request.G25AdmixtureEraId is int eraId)
        {
            era = await dbContext.G25AdmixtureEras
                .FirstOrDefaultAsync(e => e.Id == eraId, cancellationToken)
                ?? throw new ArgumentException($"G25 admixture era {eraId} not found.", nameof(request));
        }

        var now = DateTime.UtcNow;
        var entity = new G25PopulationSample
        {
            Label = request.Label.Trim(),
            Coordinates = request.Coordinates.Trim(),
            G25AdmixtureEraId = era?.Id,
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

        dbContext.G25PopulationSamples.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateG25PopulationSampleContract.Response
        {
            Id = entity.Id,
            Label = entity.Label,
            Coordinates = entity.Coordinates,
            G25AdmixtureEraId = entity.G25AdmixtureEraId,
            G25AdmixtureEra = era is null
                ? null
                : new G25AdmixtureEraSummaryDto { Id = era.Id, Name = era.Name },
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

    public async Task<GetG25PopulationSampleContract.Response?> UpdateAsync(int id, string identityId, UpdateG25PopulationSampleContract.Request request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.G25PopulationSamples
            .Include(e => e.G25AdmixtureEra)
            .Include(e => e.ResearchLinks)
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null) return null;

        G25AdmixtureEra? era = null;
        if (request.G25AdmixtureEraId is int eraId)
        {
            era = await dbContext.G25AdmixtureEras
                .FirstOrDefaultAsync(e => e.Id == eraId, cancellationToken)
                ?? throw new ArgumentException($"G25 admixture era {eraId} not found.", nameof(request));
        }

        var now = DateTime.UtcNow;
        entity.Label = request.Label.Trim();
        entity.Coordinates = request.Coordinates.Trim();
        entity.G25AdmixtureEraId = era?.Id;
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

        return new GetG25PopulationSampleContract.Response
        {
            Id = entity.Id,
            Label = entity.Label,
            Coordinates = entity.Coordinates,
            G25AdmixtureEraId = entity.G25AdmixtureEraId,
            G25AdmixtureEra = era is null
                ? null
                : new G25AdmixtureEraSummaryDto { Id = era.Id, Name = era.Name },
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
        var entity = await dbContext.G25PopulationSamples.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null) return false;

        dbContext.G25PopulationSamples.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
