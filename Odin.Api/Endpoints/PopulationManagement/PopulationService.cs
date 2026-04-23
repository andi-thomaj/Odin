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
    Task<byte[]?> GetGifAvatarImageAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error, bool NotFound)> UploadGifAvatarImageAsync(int id, IFormFile file, string identityId, CancellationToken cancellationToken = default);
    Task<bool> DeleteGifAvatarImageAsync(int id, string identityId, CancellationToken cancellationToken = default);
    Task<(int Updated, int Unmatched, int MissingOnDisk)> SyncGifAvatarsFromDiskAsync(string identityId, CancellationToken cancellationToken = default);
}

public partial class PopulationService(ApplicationDbContext dbContext, IMemoryCache cache) : IPopulationService
{
    private const string ErasCacheKey = "AllEras";
    private const long MaxGifAvatarBytes = 25 * 1024 * 1024;
    private static readonly HashSet<string> AllowedGifContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/gif"
    };

    public async Task<IReadOnlyList<GetPopulationContract.AdminResponse>> GetAllAdminAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.QpadmPopulations
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
                HasGifAvatarImage = p.GifAvatarImage != null,
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<GetPopulationContract.AdminResponse?> GetByIdAdminAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.QpadmPopulations
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
                HasGifAvatarImage = p.GifAvatarImage != null,
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(GetPopulationContract.AdminResponse? Response, string? Error)> CreateAsync(
        string identityId, CreatePopulationContract.Request request, CancellationToken cancellationToken = default)
    {
        var error = await ValidateAsync(request.Name, request.GeoJson, request.Color, request.EraId, request.MusicTrackId, existingId: null, cancellationToken);
        if (error is not null) return (null, error);

        var now = DateTime.UtcNow;
        var entity = new QpadmPopulation
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

        dbContext.QpadmPopulations.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(ErasCacheKey);

        var response = await GetByIdAdminAsync(entity.Id, cancellationToken);
        return (response, null);
    }

    public async Task<(GetPopulationContract.AdminResponse? Response, string? Error, bool NotFound)> UpdateAsync(
        int id, string identityId, UpdatePopulationContract.Request request, CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.QpadmPopulations.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
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
        var entity = await dbContext.QpadmPopulations.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (entity is null) return false;

        dbContext.QpadmPopulations.Remove(entity);
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
        var nameExists = await dbContext.QpadmPopulations
            .AsNoTracking()
            .AnyAsync(p => p.Name == nameTrim && (existingId == null || p.Id != existingId), cancellationToken);
        if (nameExists) return $"A population named '{nameTrim}' already exists.";

        var eraExists = await dbContext.QpadmEras.AsNoTracking().AnyAsync(e => e.Id == eraId, cancellationToken);
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

    public async Task<byte[]?> GetGifAvatarImageAsync(int id, CancellationToken cancellationToken = default)
    {
        return await dbContext.QpadmPopulations
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(p => p.GifAvatarImage)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<(bool Success, string? Error, bool NotFound)> UploadGifAvatarImageAsync(
        int id, IFormFile file, string identityId, CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
            return (false, "Uploaded file is empty.", false);
        if (file.Length > MaxGifAvatarBytes)
            return (false, $"File exceeds the {MaxGifAvatarBytes / (1024 * 1024)} MB limit.", false);
        if (!AllowedGifContentTypes.Contains(file.ContentType))
            return (false, $"Invalid content type '{file.ContentType}'. Allowed: image/gif.", false);

        var population = await dbContext.QpadmPopulations.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (population is null) return (false, null, true);

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, cancellationToken);

        population.GifAvatarImage = ms.ToArray();
        population.UpdatedAt = DateTime.UtcNow;
        population.UpdatedBy = identityId;

        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(ErasCacheKey);
        return (true, null, false);
    }

    public async Task<bool> DeleteGifAvatarImageAsync(int id, string identityId, CancellationToken cancellationToken = default)
    {
        var population = await dbContext.QpadmPopulations.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (population is null) return false;

        population.GifAvatarImage = null;
        population.UpdatedAt = DateTime.UtcNow;
        population.UpdatedBy = identityId;

        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(ErasCacheKey);
        return true;
    }

    public async Task<(int Updated, int Unmatched, int MissingOnDisk)> SyncGifAvatarsFromDiskAsync(
        string identityId, CancellationToken cancellationToken = default)
    {
        var gifDir = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", "population-gifs");
        if (!Directory.Exists(gifDir))
            return (0, 0, 0);

        var populations = await dbContext.QpadmPopulations.ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var updated = 0;
        var missingOnDisk = 0;

        foreach (var population in populations)
        {
            var filePath = Path.Combine(gifDir, $"{population.Name}.gif");
            if (!File.Exists(filePath))
            {
                missingOnDisk++;
                continue;
            }

            population.GifAvatarImage = await File.ReadAllBytesAsync(filePath, cancellationToken);
            population.UpdatedAt = now;
            population.UpdatedBy = identityId;
            updated++;
        }

        if (updated > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            cache.Remove(ErasCacheKey);
        }

        var populationNames = new HashSet<string>(populations.Select(p => p.Name), StringComparer.Ordinal);
        var unmatched = Directory.EnumerateFiles(gifDir, "*.gif")
            .Select(Path.GetFileNameWithoutExtension)
            .Count(name => name is not null && !populationNames.Contains(name));

        return (updated, unmatched, missingOnDisk);
    }
}
