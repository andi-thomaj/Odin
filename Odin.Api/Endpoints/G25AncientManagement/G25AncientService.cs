using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25AncientManagement.Models;

namespace Odin.Api.Endpoints.G25AncientManagement;

public interface IG25AncientService
{
    Task<GetG25AncientContract.PagedResponse> GetPagedAsync(int page, int pageSize, CancellationToken cancellationToken = default);
    Task<GetG25AncientContract.Response?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CreateG25AncientContract.Response> CreateAsync(string identityId, CreateG25AncientContract.Request request,
        CancellationToken cancellationToken = default);
    Task<GetG25AncientContract.Response?> UpdateAsync(int id, string identityId, UpdateG25AncientContract.Request request,
        CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public class G25AncientService(ApplicationDbContext dbContext) : IG25AncientService
{
    private const int MaxPageSize = 200;

    public async Task<GetG25AncientContract.PagedResponse> GetPagedAsync(int page, int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 25;
        if (pageSize > MaxPageSize) pageSize = MaxPageSize;

        var query = dbContext.G25Ancients.AsNoTracking();

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(e => e.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => new GetG25AncientContract.Response
            {
                Id = e.Id,
                Label = e.Label,
                Coordinates = e.Coordinates
            })
            .ToListAsync(cancellationToken);

        return new GetG25AncientContract.PagedResponse
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<GetG25AncientContract.Response?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.G25Ancients
            .AsNoTracking()
            .Where(e => e.Id == id)
            .Select(e => new GetG25AncientContract.Response
            {
                Id = e.Id,
                Label = e.Label,
                Coordinates = e.Coordinates
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CreateG25AncientContract.Response> CreateAsync(string identityId, CreateG25AncientContract.Request request,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entity = new G25Ancient
        {
            Label = request.Label.Trim(),
            Coordinates = request.Coordinates.Trim(),
            CreatedAt = now,
            CreatedBy = identityId,
            UpdatedAt = now,
            UpdatedBy = identityId
        };

        dbContext.G25Ancients.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new CreateG25AncientContract.Response
        {
            Id = entity.Id,
            Label = entity.Label,
            Coordinates = entity.Coordinates
        };
    }

    public async Task<GetG25AncientContract.Response?> UpdateAsync(int id, string identityId, UpdateG25AncientContract.Request request,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.G25Ancients.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null) return null;

        var now = DateTime.UtcNow;
        entity.Label = request.Label.Trim();
        entity.Coordinates = request.Coordinates.Trim();
        entity.UpdatedAt = now;
        entity.UpdatedBy = identityId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GetG25AncientContract.Response
        {
            Id = entity.Id,
            Label = entity.Label,
            Coordinates = entity.Coordinates
        };
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.G25Ancients.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);
        if (entity is null) return false;

        dbContext.G25Ancients.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
