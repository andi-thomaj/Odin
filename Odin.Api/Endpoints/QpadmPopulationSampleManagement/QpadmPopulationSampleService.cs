using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.QpadmPopulationSampleManagement.Models;

namespace Odin.Api.Endpoints.QpadmPopulationSampleManagement;

public interface IQpadmPopulationSampleService
{
    Task<IReadOnlyList<GetQpadmPopulationSampleContract.Response>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<GetQpadmPopulationSampleContract.PagedResponse> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<GetQpadmPopulationSampleContract.Response?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CreateQpadmPopulationSampleContract.Response> CreateAsync(string identityId, CreateQpadmPopulationSampleContract.Request request,
        CancellationToken cancellationToken = default);
    Task<GetQpadmPopulationSampleContract.Response?> UpdateAsync(int id, string identityId, UpdateQpadmPopulationSampleContract.Request request,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> SearchLabelsAsync(string query, int limit = 50, CancellationToken cancellationToken = default);
}

public class QpadmPopulationSampleService(ApplicationDbContext dbContext) : IQpadmPopulationSampleService
{
    private const int MaxPageSize = 200;
    private const int MaxSearchLimit = 200;

    public async Task<IReadOnlyList<GetQpadmPopulationSampleContract.Response>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.QpadmPopulationSamples
            .AsNoTracking()
            .OrderBy(e => e.Id)
            .Select(e => new GetQpadmPopulationSampleContract.Response
            {
                Id = e.Id,
                Label = e.Label,
                Coordinates = e.Coordinates,
                ResearchLinks = e.ResearchLinks
                    .OrderBy(r => r.Id)
                    .Select(r => new QpadmResearchLinkDto.Response
                    {
                        Id = r.Id,
                        Label = r.Label,
                        Link = r.Link
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<GetQpadmPopulationSampleContract.PagedResponse> GetPagedAsync(int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var query = dbContext.QpadmPopulationSamples.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new GetQpadmPopulationSampleContract.Response
            {
                Id = e.Id,
                Label = e.Label,
                Coordinates = e.Coordinates,
                ResearchLinks = e.ResearchLinks
                    .OrderBy(r => r.Id)
                    .Select(r => new QpadmResearchLinkDto.Response
                    {
                        Id = r.Id,
                        Label = r.Label,
                        Link = r.Link
                    })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        return new GetQpadmPopulationSampleContract.PagedResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<GetQpadmPopulationSampleContract.Response?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.QpadmPopulationSamples
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new GetQpadmPopulationSampleContract.Response
            {
                Id = e.Id,
                Label = e.Label,
                Coordinates = e.Coordinates,
                ResearchLinks = e.ResearchLinks
                    .OrderBy(r => r.Id)
                    .Select(r => new QpadmResearchLinkDto.Response
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

        return await dbContext.QpadmPopulationSamples
            .AsNoTracking()
            .Where(e => e.Label.ToLower().Contains(lower))
            .OrderBy(e => e.Id)
            .Take(limit)
            .Select(e => e.Label)
            .ToListAsync(cancellationToken);
    }

    public async Task<CreateQpadmPopulationSampleContract.Response> CreateAsync(string identityId, CreateQpadmPopulationSampleContract.Request request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entity = new QpadmPopulationSample
        {
            Label = request.Label.Trim(),
            Coordinates = request.Coordinates.Trim(),
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

        dbContext.QpadmPopulationSamples.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateQpadmPopulationSampleContract.Response
        {
            Id = entity.Id,
            Label = entity.Label,
            Coordinates = entity.Coordinates,
            ResearchLinks = entity.ResearchLinks
                .OrderBy(r => r.Id)
                .Select(r => new QpadmResearchLinkDto.Response
                {
                    Id = r.Id,
                    Label = r.Label,
                    Link = r.Link
                })
                .ToList()
        };
    }

    public async Task<GetQpadmPopulationSampleContract.Response?> UpdateAsync(int id, string identityId, UpdateQpadmPopulationSampleContract.Request request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.QpadmPopulationSamples.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null) return null;

        var now = DateTime.UtcNow;
        entity.Label = request.Label.Trim();
        entity.Coordinates = request.Coordinates.Trim();
        entity.UpdatedAt = now;
        entity.UpdatedBy = identityId;

        await dbContext.SaveChangesAsync(cancellationToken);

        var links = await dbContext.ResearchLinks
            .AsNoTracking()
            .Where(r => r.QpadmPopulationSampleId == entity.Id)
            .OrderBy(r => r.Id)
            .Select(r => new QpadmResearchLinkDto.Response
            {
                Id = r.Id,
                Label = r.Label,
                Link = r.Link
            })
            .ToListAsync(cancellationToken);

        return new GetQpadmPopulationSampleContract.Response
        {
            Id = entity.Id,
            Label = entity.Label,
            Coordinates = entity.Coordinates,
            ResearchLinks = links
        };
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.QpadmPopulationSamples.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null) return false;

        dbContext.QpadmPopulationSamples.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
