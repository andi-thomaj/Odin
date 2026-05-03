using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.PopulationManagement.Models;
using Odin.Api.Storage;

namespace Odin.Api.Endpoints.PopulationManagement;

public interface IPopulationService
{
    Task<IReadOnlyList<GetPopulationContract.AdminResponse>> GetAllAdminAsync(CancellationToken cancellationToken = default);
    Task<GetPopulationContract.AdminResponse?> GetByIdAdminAsync(int id, CancellationToken cancellationToken = default);
    Task<(GetPopulationContract.AdminResponse? Response, string? Error)> CreateAsync(string identityId, CreatePopulationContract.Request request, CancellationToken cancellationToken = default);
    Task<(GetPopulationContract.AdminResponse? Response, string? Error, bool NotFound)> UpdateAsync(int id, string identityId, UpdatePopulationContract.Request request, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<(bool Success, string? Error, bool NotFound)> UploadVideoAvatarAsync(int id, IFormFile file, string identityId, CancellationToken cancellationToken = default);
    Task<bool> DeleteVideoAvatarAsync(int id, string identityId, CancellationToken cancellationToken = default);
    Task<(int Updated, int Unmatched, int MissingOnDisk, int Failed)> SyncVideoAvatarsFromDiskAsync(string identityId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GetPopulationContract.VideoAvatarListItem>> GetVideoAvatarsListAsync(CancellationToken cancellationToken = default);
}

public partial class PopulationService(
    ApplicationDbContext dbContext,
    IMemoryCache cache,
    IR2Storage r2Storage,
    ILogger<PopulationService> logger) : IPopulationService
{
    private const string ErasCacheKey = "AllEras";
    private const long MaxVideoAvatarBytes = 25 * 1024 * 1024;
    private const string VideoContentType = "video/mp4";
    private static readonly HashSet<string> AllowedVideoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        VideoContentType,
    };

    /// <summary>R2 object key for a population's MP4 avatar. ID-stable: renaming a population doesn't break the URL.
    /// The <c>qpAdm/population-videos/</c> prefix scopes this service's objects inside the shared
    /// <c>ancestrify</c> bucket (G25 service uses sibling prefixes under <c>qpAdm/</c> and its own roots).</summary>
    private static string AvatarKey(int populationId) => $"qpAdm/population-videos/{populationId}.mp4";

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
                HasVideoAvatar = p.VideoAvatarVersion != null,
                VideoVersion = p.VideoAvatarVersion != null ? p.VideoAvatarVersion.Value.ToString() : null,
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
                HasVideoAvatar = p.VideoAvatarVersion != null,
                VideoVersion = p.VideoAvatarVersion != null ? p.VideoAvatarVersion.Value.ToString() : null,
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

        // If the population had an avatar in R2, remove the orphan object too. Best-effort:
        // a failed R2 delete shouldn't block the row deletion.
        if (entity.VideoAvatarVersion is not null)
        {
            try
            {
                await r2Storage.DeleteAsync(AvatarKey(id), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to remove R2 avatar for deleted population {Id}; continuing", id);
            }
        }

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

    public async Task<(bool Success, string? Error, bool NotFound)> UploadVideoAvatarAsync(
        int id, IFormFile file, string identityId, CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
            return (false, "Uploaded file is empty.", false);
        if (file.Length > MaxVideoAvatarBytes)
            return (false, $"File exceeds the {MaxVideoAvatarBytes / (1024 * 1024)} MB limit.", false);
        if (!AllowedVideoContentTypes.Contains(file.ContentType))
            return (false, $"Invalid content type '{file.ContentType}'. Allowed: video/mp4.", false);

        var population = await dbContext.QpadmPopulations.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (population is null) return (false, null, true);

        await using (var stream = file.OpenReadStream())
        {
            await r2Storage.UploadAsync(AvatarKey(id), stream, VideoContentType, cancellationToken);
        }

        var now = DateTime.UtcNow;
        population.VideoAvatarVersion = now.Ticks;
        population.UpdatedAt = now;
        population.UpdatedBy = identityId;

        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(ErasCacheKey);
        return (true, null, false);
    }

    public async Task<bool> DeleteVideoAvatarAsync(int id, string identityId, CancellationToken cancellationToken = default)
    {
        var population = await dbContext.QpadmPopulations.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (population is null) return false;

        await r2Storage.DeleteAsync(AvatarKey(id), cancellationToken);

        population.VideoAvatarVersion = null;
        population.UpdatedAt = DateTime.UtcNow;
        population.UpdatedBy = identityId;

        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(ErasCacheKey);
        return true;
    }

    public async Task<IReadOnlyList<GetPopulationContract.VideoAvatarListItem>> GetVideoAvatarsListAsync(CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.QpadmPopulations
            .AsNoTracking()
            .Where(p => p.VideoAvatarVersion != null)
            .OrderByDescending(p => p.Era.Id).ThenBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, Version = p.VideoAvatarVersion!.Value })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new GetPopulationContract.VideoAvatarListItem
        {
            Id = r.Id,
            Name = r.Name,
            Version = r.Version.ToString(),
            Url = $"{r2Storage.GetPublicUrl(AvatarKey(r.Id))}?v={r.Version}",
        }).ToList();
    }

    /// <summary>
    /// Bulk-uploads each <c>Data/SeedData/population-videos/{Name}.mp4</c> to R2 under the
    /// stable key <c>qpAdm/population-videos/{Id}.mp4</c>, then bumps <see cref="QpadmPopulation.VideoAvatarVersion"/>
    /// so clients re-fetch with a fresh cache-bust query. Disk filenames still use the population
    /// <c>Name</c> (curator-friendly); the R2 key uses the population <c>Id</c> (rename-safe).
    /// </summary>
    public async Task<(int Updated, int Unmatched, int MissingOnDisk, int Failed)> SyncVideoAvatarsFromDiskAsync(
        string identityId, CancellationToken cancellationToken = default)
    {
        var videoDir = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", "population-videos");
        if (!Directory.Exists(videoDir))
        {
            logger.LogWarning("Video sync requested but {Dir} does not exist.", videoDir);
            return (0, 0, 0, 0);
        }

        var populations = await dbContext.QpadmPopulations.ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        var updated = 0;
        var missingOnDisk = 0;
        var failed = 0;

        foreach (var population in populations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = Path.Combine(videoDir, $"{population.Name}.mp4");
            if (!File.Exists(filePath))
            {
                missingOnDisk++;
                continue;
            }

            try
            {
                await using var fs = File.OpenRead(filePath);
                if (fs.Length == 0)
                {
                    failed++;
                    continue;
                }
                await r2Storage.UploadAsync(AvatarKey(population.Id), fs, VideoContentType, cancellationToken);
            }
            catch (Exception ex)
            {
                // Error level (not Warning) so the Serilog Postgres sink captures the exception —
                // makes production debugging via the `logs` table possible without Coolify shell access.
                logger.LogError(ex,
                    "Failed to upload MP4 for population {Name} (id {Id}) to R2", population.Name, population.Id);
                failed++;
                continue;
            }

            population.VideoAvatarVersion = now.Ticks;
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
        var unmatched = Directory.EnumerateFiles(videoDir, "*.mp4")
            .Select(Path.GetFileNameWithoutExtension)
            .Count(name => name is not null && !populationNames.Contains(name));

        return (updated, unmatched, missingOnDisk, failed);
    }
}
