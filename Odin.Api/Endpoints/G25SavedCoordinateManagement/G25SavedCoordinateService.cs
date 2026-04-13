using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25SavedCoordinateManagement.Models;

namespace Odin.Api.Endpoints.G25SavedCoordinateManagement;

public interface IG25SavedCoordinateService
{
    Task<IReadOnlyList<GetG25SavedCoordinateContract.Response>> GetAllForUserAsync(int userId,
        CancellationToken cancellationToken = default);

    Task<GetG25SavedCoordinateContract.Response?> GetByIdForUserAsync(int id, int userId,
        CancellationToken cancellationToken = default);

    Task<GetG25SavedCoordinateContract.Response> CreateAsync(int userId, string identityId,
        CreateG25SavedCoordinateContract.Request request, CancellationToken cancellationToken = default);

    Task<GetG25SavedCoordinateContract.Response?> UpdateAsync(int id, int userId, string identityId,
        UpdateG25SavedCoordinateContract.Request request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default);
}

public class G25SavedCoordinateService(ApplicationDbContext dbContext) : IG25SavedCoordinateService
{
    private const int MaxTitleLength = 200;
    private const int MaxAddModeLength = 32;
    private const int MaxViewIdLength = 64;
    private const int MaxCustomNameLength = 200;

    public async Task<IReadOnlyList<GetG25SavedCoordinateContract.Response>> GetAllForUserAsync(int userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.G25SavedCoordinates
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.UpdatedAt)
            .Select(e => new GetG25SavedCoordinateContract.Response
            {
                Id = e.Id,
                Title = e.Title,
                RawInput = e.RawInput,
                Scaling = e.Scaling,
                AddMode = e.AddMode,
                CustomName = e.CustomName,
                ViewId = e.ViewId,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<GetG25SavedCoordinateContract.Response?> GetByIdForUserAsync(int id, int userId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.G25SavedCoordinates
            .AsNoTracking()
            .Where(e => e.Id == id && e.UserId == userId)
            .Select(e => new GetG25SavedCoordinateContract.Response
            {
                Id = e.Id,
                Title = e.Title,
                RawInput = e.RawInput,
                Scaling = e.Scaling,
                AddMode = e.AddMode,
                CustomName = e.CustomName,
                ViewId = e.ViewId,
                CreatedAt = e.CreatedAt,
                UpdatedAt = e.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<GetG25SavedCoordinateContract.Response> CreateAsync(int userId, string identityId,
        CreateG25SavedCoordinateContract.Request request, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var entity = new G25SavedCoordinate
        {
            UserId = userId,
            Title = Truncate(request.Title.Trim(), MaxTitleLength),
            RawInput = request.RawInput.Trim(),
            Scaling = request.Scaling,
            AddMode = Truncate(request.AddMode.Trim(), MaxAddModeLength),
            CustomName = string.IsNullOrWhiteSpace(request.CustomName)
                ? null
                : Truncate(request.CustomName.Trim(), MaxCustomNameLength),
            ViewId = Truncate(request.ViewId.Trim(), MaxViewIdLength),
            CreatedAt = now,
            CreatedBy = identityId,
            UpdatedAt = now,
            UpdatedBy = identityId
        };

        dbContext.G25SavedCoordinates.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new GetG25SavedCoordinateContract.Response
        {
            Id = entity.Id,
            Title = entity.Title,
            RawInput = entity.RawInput,
            Scaling = entity.Scaling,
            AddMode = entity.AddMode,
            CustomName = entity.CustomName,
            ViewId = entity.ViewId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task<GetG25SavedCoordinateContract.Response?> UpdateAsync(int id, int userId, string identityId,
        UpdateG25SavedCoordinateContract.Request request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.G25SavedCoordinates
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, cancellationToken);
        if (entity is null) return null;

        entity.Title = Truncate(request.Title.Trim(), MaxTitleLength);
        entity.RawInput = request.RawInput.Trim();
        entity.Scaling = request.Scaling;
        entity.AddMode = Truncate(request.AddMode.Trim(), MaxAddModeLength);
        entity.CustomName = string.IsNullOrWhiteSpace(request.CustomName)
            ? null
            : Truncate(request.CustomName.Trim(), MaxCustomNameLength);
        entity.ViewId = Truncate(request.ViewId.Trim(), MaxViewIdLength);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = identityId;

        await dbContext.SaveChangesAsync(cancellationToken);

        return new GetG25SavedCoordinateContract.Response
        {
            Id = entity.Id,
            Title = entity.Title,
            RawInput = entity.RawInput,
            Scaling = entity.Scaling,
            AddMode = entity.AddMode,
            CustomName = entity.CustomName,
            ViewId = entity.ViewId,
            CreatedAt = entity.CreatedAt,
            UpdatedAt = entity.UpdatedAt
        };
    }

    public async Task<bool> DeleteAsync(int id, int userId, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.G25SavedCoordinates
            .FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, cancellationToken);
        if (entity is null) return false;

        dbContext.G25SavedCoordinates.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
