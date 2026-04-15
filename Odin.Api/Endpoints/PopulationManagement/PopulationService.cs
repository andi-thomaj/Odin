using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.PopulationManagement.Models;

namespace Odin.Api.Endpoints.PopulationManagement;

public interface IPopulationService
{
    Task<IReadOnlyList<GetPopulationContract.AdminResponse>> GetAllAdminAsync(CancellationToken cancellationToken = default);
    Task<GetPopulationContract.AdminResponse?> GetByIdAdminAsync(int id, CancellationToken cancellationToken = default);
    Task<(GetPopulationContract.AdminResponse? Response, string? Error)> CreateAsync(string identityId, CreatePopulationContract.Request request, CancellationToken cancellationToken = default);
    Task<(GetPopulationContract.AdminResponse? Response, string? Error, bool NotFound)> UpdateAsync(int id, string identityId, UpdatePopulationContract.Request request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public partial class PopulationService(ApplicationDbContext dbContext, IMemoryCache cache) : IPopulationService
{
    private const string ErasCacheKey = "AllEras";

    public async Task<IReadOnlyList<GetPopulationContract.AdminResponse>> GetAllAdminAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Populations
            .AsNoTracking()
            .OrderBy(p => p.Era.Id).ThenBy(p => p.Name)
            .Select(p => new GetPopulationContract.AdminResponse
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                GeoJson = p.GeoJson,
                IconFileName = p.IconFileName,
                Color = p.Color,
                EraId = p.EraId,
                EraName = p.Era.Name,
                MusicTrackId = p.MusicTrackId,
                MusicTrackName = p.MusicTrack.Name,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<GetPopulationContract.AdminResponse?> GetByIdAdminAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.Populations
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => new GetPopulationContract.AdminResponse
            {
                Id = p.Id,
                Name = p.Name,
                Description = p.Description,
                GeoJson = p.GeoJson,
                IconFileName = p.IconFileName,
                Color = p.Color,
                EraId = p.EraId,
                EraName = p.Era.Name,
                MusicTrackId = p.MusicTrackId,
                MusicTrackName = p.MusicTrack.Name,
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(GetPopulationContract.AdminResponse? Response, string? Error)> CreateAsync(
        string identityId, CreatePopulationContract.Request request, CancellationToken cancellationToken = default)
    {
        var error = await ValidateAsync(request.Name, request.GeoJson, request.Color, request.EraId, request.MusicTrackId, existingId: null, cancellationToken);
        if (error is not null) return (null, error);

        var now = DateTime.UtcNow;
        var entity = new Population
        {
            Name = request.Name.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            GeoJson = request.GeoJson.Trim(),
            IconFileName = request.IconFileName?.Trim() ?? string.Empty,
            Color = request.Color.Trim(),
            EraId = request.EraId,
            MusicTrackId = request.MusicTrackId,
            CreatedAt = now,
            CreatedBy = identityId,
            UpdatedAt = now,
            UpdatedBy = identityId,
        };

        dbContext.Populations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(ErasCacheKey);

        var response = await GetByIdAdminAsync(entity.Id, cancellationToken);
        return (response, null);
    }

    public async Task<(GetPopulationContract.AdminResponse? Response, string? Error, bool NotFound)> UpdateAsync(
        int id, string identityId, UpdatePopulationContract.Request request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Populations.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity is null) return (null, null, true);

        var error = await ValidateAsync(request.Name, request.GeoJson, request.Color, request.EraId, request.MusicTrackId, existingId: id, cancellationToken);
        if (error is not null) return (null, error, false);

        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim() ?? string.Empty;
        entity.GeoJson = request.GeoJson.Trim();
        entity.IconFileName = request.IconFileName?.Trim() ?? string.Empty;
        entity.Color = request.Color.Trim();
        entity.EraId = request.EraId;
        entity.MusicTrackId = request.MusicTrackId;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = identityId;

        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(ErasCacheKey);

        var response = await GetByIdAdminAsync(entity.Id, cancellationToken);
        return (response, null, false);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.Populations.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity is null) return false;

        dbContext.Populations.Remove(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(ErasCacheKey);
        return true;
    }

    private async Task<string?> ValidateAsync(
        string name, string geoJson, string color, int eraId, int musicTrackId, int? existingId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100)
            return "Name is required and must be 1–100 characters.";
        if (string.IsNullOrWhiteSpace(geoJson))
            return "GeoJson is required.";
        if (!IsValidGeoJson(geoJson))
            return "GeoJson must be a valid Polygon or MultiPolygon.";
        if (!IsValidHexColor(color))
            return "Color must be a 7-character hex value (e.g. #RRGGBB).";

        var nameTrim = name.Trim();
        var nameExists = await dbContext.Populations
            .AsNoTracking()
            .AnyAsync(p => p.Name == nameTrim && (existingId == null || p.Id != existingId), cancellationToken);
        if (nameExists) return $"A population named '{nameTrim}' already exists.";

        var eraExists = await dbContext.Eras.AsNoTracking().AnyAsync(e => e.Id == eraId, cancellationToken);
        if (!eraExists) return $"Era with id {eraId} does not exist.";

        var trackExists = await dbContext.MusicTracks.AsNoTracking().AnyAsync(t => t.Id == musicTrackId, cancellationToken);
        if (!trackExists) return $"Music track with id {musicTrackId} does not exist.";

        return null;
    }

    private static bool IsValidGeoJson(string geoJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(geoJson);
            if (!doc.RootElement.TryGetProperty("type", out var typeProp)) return false;
            var type = typeProp.GetString();
            if (type != "Polygon" && type != "MultiPolygon") return false;
            if (!doc.RootElement.TryGetProperty("coordinates", out var coords)) return false;
            return coords.ValueKind == JsonValueKind.Array && coords.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static bool IsValidHexColor(string color) => HexColorRegex().IsMatch(color?.Trim() ?? "");

    [GeneratedRegex(@"^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorRegex();
}
