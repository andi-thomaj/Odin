using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25TargetCoordinateManagement.Models;

namespace Odin.Api.Endpoints.G25TargetCoordinateManagement;

public interface IG25TargetCoordinateService
{
    Task<IReadOnlyList<GetG25TargetCoordinateContract.Summary>> GetAllForUserAsync(int userId,
        CancellationToken cancellationToken = default);

    Task<GetG25TargetCoordinateContract.Response?> GetByIdForUserAsync(int id, int userId,
        CancellationToken cancellationToken = default);

    Task<GetG25TargetCoordinateContract.Summary> CreateAsync(int userId, string identityId,
        CreateG25TargetCoordinateContract.Request request, CancellationToken cancellationToken = default);

    Task<GetG25TargetCoordinateContract.Summary?> UpdateAsync(int id, int userId, string identityId,
        UpdateG25TargetCoordinateContract.Request request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default);
}

public class G25TargetCoordinateService(ApplicationDbContext dbContext) : IG25TargetCoordinateService
{
    private const int MaxLabelLength = 500;

    public async Task<IReadOnlyList<GetG25TargetCoordinateContract.Summary>> GetAllForUserAsync(int userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.G25TargetCoordinates
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.UpdatedAt)
            .Select(e => new GetG25TargetCoordinateContract.Summary
            {
                Id = e.Id,
                Label = e.Label,
                LineCount = e.Coordinates.Length == 0
                    ? 0
                    : e.Coordinates.Length - e.Coordinates.Replace("\n", "").Length + 1,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<GetG25TargetCoordinateContract.Response?> GetByIdForUserAsync(int id, int userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.G25TargetCoordinates
            .AsNoTracking()
            .Where(e => e.Id == id && e.UserId == userId)
            .Select(e => new GetG25TargetCoordinateContract.Response
            {
                Id = e.Id,
                Label = e.Label,
                Coordinates = e.Coordinates,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<GetG25TargetCoordinateContract.Summary> CreateAsync(int userId, string identityId,
        CreateG25TargetCoordinateContract.Request request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var coordinates = request.Coordinates ?? string.Empty;
        var entity = new G25TargetCoordinate
        {
            UserId = userId,
            Label = Truncate(request.Label.Trim(), MaxLabelLength),
            Coordinates = coordinates,
            CreatedAt = now,
            CreatedBy = identityId,
            UpdatedAt = now,
            UpdatedBy = identityId
        };

        dbContext.G25TargetCoordinates.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new GetG25TargetCoordinateContract.Summary
        {
            Id = entity.Id,
            Label = entity.Label,
            LineCount = coordinates.Length == 0 ? 0 : coordinates.Count(c => c == '\n') + 1,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task<GetG25TargetCoordinateContract.Summary?> UpdateAsync(int id, int userId, string identityId,
        UpdateG25TargetCoordinateContract.Request request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.G25TargetCoordinates
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, cancellationToken);
        if (entity is null) return null;

        entity.Label = Truncate(request.Label.Trim(), MaxLabelLength);
        entity.Coordinates = request.Coordinates ?? string.Empty;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = identityId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GetG25TargetCoordinateContract.Summary
        {
            Id = entity.Id,
            Label = entity.Label,
            LineCount = entity.Coordinates.Length == 0 ? 0 : entity.Coordinates.Count(c => c == '\n') + 1,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task<bool> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.G25TargetCoordinates
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, cancellationToken);
        if (entity is null) return false;

        dbContext.G25TargetCoordinates.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
