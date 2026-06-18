using System.Globalization;
using System.Text;
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
    Task<(bool Success, string? Error, bool NotFound)> UploadKeyframeAsync(int id, IFormFile file, string identityId, CancellationToken cancellationToken = default);
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
    private const long MaxKeyframeBytes = 10 * 1024 * 1024;
    private const string KeyframeContentType = "image/webp";
    private static readonly HashSet<string> AllowedKeyframeContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        KeyframeContentType,
    };

    /// <summary>Allowed Higgsfield video models → their valid "mode" values. Must stay in sync with the
    /// frontend's <c>VIDEO_MODEL_MODES</c> in <c>ancestry-video-media/manifest.ts</c> and the media
    /// server's per-model arg builders.</summary>
    private static readonly Dictionary<string, string[]> VideoModelModes = new(StringComparer.Ordinal)
    {
        ["seedance_2_0"] = ["std", "fast"],
        ["kling3_0"] = ["pro", "std", "4k"],
    };
    private static readonly int[] AllowedVideoDurations = [5, 10];

    /// <summary>Trim a prompt, collapsing blank/whitespace to <c>null</c> ("use the frontend default template").</summary>
    private static string? NormalizePrompt(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    /// <summary>Keep a video model only if it's a known id, else <c>null</c> (default).</summary>
    private static string? NormalizeVideoModel(string? model)
    {
        var m = model?.Trim();
        return !string.IsNullOrEmpty(m) && VideoModelModes.ContainsKey(m) ? m : null;
    }

    /// <summary>Keep a mode only if valid for the (normalized) model, else <c>null</c> (model default).</summary>
    private static string? NormalizeVideoMode(string? model, string? mode)
    {
        var m = NormalizeVideoModel(model);
        var mo = mode?.Trim();
        if (m is null || string.IsNullOrEmpty(mo)) return null;
        return VideoModelModes[m].Contains(mo, StringComparer.Ordinal) ? mo : null;
    }

    /// <summary>Keep a duration only if it's an allowed value (5/10), else <c>null</c> (default 5).</summary>
    private static int? NormalizeVideoDuration(int? duration) =>
        duration is { } d && AllowedVideoDurations.Contains(d) ? d : null;

    /// <summary>R2 object key for a population's MP4 avatar, derived from the population's Name.
    /// Name-based (not ID-based) so the (name → video) pairing is stable across DB reseeds and
    /// across environments sharing the same R2 bucket — an ID-keyed scheme would silently misalign
    /// videos with names whenever auto-generated IDs shifted between environments. Renames are
    /// handled explicitly by <see cref="UpdateAsync"/>, which copies the R2 object to the new key.
    /// The <c>qpAdm/population-videos/</c> prefix scopes this service's objects inside the shared
    /// <c>ancestrify</c> bucket.</summary>
    public static string AvatarKey(string populationName) =>
        $"qpAdm/population-videos/{Slugify(populationName)}.mp4";

    /// <summary>R2 object key for a population's keyframe WEBP — the still that the avatar video is
    /// animated from. Same slug scheme as <see cref="AvatarKey"/> (just the <c>.webp</c> sibling).</summary>
    public static string KeyframeKey(string populationName) =>
        $"qpAdm/population-videos/{Slugify(populationName)}.webp";

    /// <summary>URL-safe, ASCII-folded, lowercase slug. Must stay byte-for-byte identical to the
    /// frontend's <c>slugifyPopulationName</c> in <c>odin-react/src/api/populations.ts</c> —
    /// the two encodings address the same R2 objects.</summary>
    internal static string Slugify(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Population name cannot be null or whitespace.", nameof(name));

        var normalized = name.Normalize(NormalizationForm.FormKD);
        var sb = new StringBuilder(normalized.Length);
        var prevWasSeparator = true;
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
                continue;
            if (ch is >= '0' and <= '9' or >= 'a' and <= 'z')
            {
                sb.Append(ch);
                prevWasSeparator = false;
            }
            else if (ch is >= 'A' and <= 'Z')
            {
                sb.Append((char)(ch + 32));
                prevWasSeparator = false;
            }
            else if (!prevWasSeparator)
            {
                sb.Append('-');
                prevWasSeparator = true;
            }
        }
        if (sb.Length > 0 && sb[^1] == '-') sb.Length--;
        return sb.ToString();
    }

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
                KeyframeVersion = p.KeyframeVersion != null ? p.KeyframeVersion.Value.ToString() : null,
                ImagePrompt = p.ImagePrompt,
                VideoPrompt = p.VideoPrompt,
                VideoModel = p.VideoModel,
                VideoMode = p.VideoMode,
                VideoDurationSeconds = p.VideoDurationSeconds,
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
                KeyframeVersion = p.KeyframeVersion != null ? p.KeyframeVersion.Value.ToString() : null,
                ImagePrompt = p.ImagePrompt,
                VideoPrompt = p.VideoPrompt,
                VideoModel = p.VideoModel,
                VideoMode = p.VideoMode,
                VideoDurationSeconds = p.VideoDurationSeconds,
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
            ImagePrompt = NormalizePrompt(request.ImagePrompt),
            VideoPrompt = NormalizePrompt(request.VideoPrompt),
            VideoModel = NormalizeVideoModel(request.VideoModel),
            VideoMode = NormalizeVideoMode(request.VideoModel, request.VideoMode),
            VideoDurationSeconds = NormalizeVideoDuration(request.VideoDurationSeconds),
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

        var oldName = entity.Name;
        var newName = request.Name.Trim();
        var oldKey = AvatarKey(oldName);
        var newKey = AvatarKey(newName);
        var nameChanged = !string.Equals(oldKey, newKey, StringComparison.Ordinal);
        var hadAvatar = entity.VideoAvatarVersion is not null;
        var hadKeyframe = entity.KeyframeVersion is not null;

        // R2 keys derive from the slugified Name, so a rename that changes the slug must move
        // the avatar object so the URL the frontend reads keeps pointing at the right bytes.
        // Copy-then-delete (rather than re-uploading) — we don't have the source MP4 here, only
        // the bucket reference. Done before the DB write so a copy failure aborts the update;
        // the orphan delete is best-effort.
        if (nameChanged && hadAvatar)
        {
            try
            {
                await r2Storage.CopyAsync(oldKey, newKey, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Population {Id} rename '{Old}' → '{New}' failed: could not copy R2 avatar to new key",
                    id, oldName, newName);
                return (null, "Failed to move the population's video avatar to the new key. The name was not changed.", false);
            }

            try
            {
                await r2Storage.DeleteAsync(oldKey, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Population {Id} renamed but the old R2 avatar key '{OldKey}' could not be deleted; orphan will remain",
                    id, oldKey);
            }
        }

        // Move the keyframe webp the same way on a slug-changing rename. Best-effort (the keyframe is a
        // secondary asset) — a failure just leaves the thumbnail stale until it's re-published.
        if (nameChanged && hadKeyframe)
        {
            var oldWebpKey = KeyframeKey(oldName);
            var newWebpKey = KeyframeKey(newName);
            try
            {
                await r2Storage.CopyAsync(oldWebpKey, newWebpKey, cancellationToken);
                try
                {
                    await r2Storage.DeleteAsync(oldWebpKey, cancellationToken);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Population {Id} renamed but the old keyframe key '{OldKey}' could not be deleted; orphan will remain",
                        id, oldWebpKey);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Population {Id} rename: could not move keyframe webp to the new key; the thumbnail may be stale until re-published",
                    id);
            }
        }

        entity.Name = newName;
        entity.Description = request.Description?.Trim() ?? string.Empty;
        entity.GeoJson = request.GeoJson.Trim();
        entity.IconFileName = request.IconFileName?.Trim() ?? string.Empty;
        entity.Color = request.Color.Trim();
        entity.EraId = request.EraId;
        entity.MusicTrackId = request.MusicTrackId;
        entity.ImagePrompt = NormalizePrompt(request.ImagePrompt);
        entity.VideoPrompt = NormalizePrompt(request.VideoPrompt);
        entity.VideoModel = NormalizeVideoModel(request.VideoModel);
        entity.VideoMode = NormalizeVideoMode(request.VideoModel, request.VideoMode);
        entity.VideoDurationSeconds = NormalizeVideoDuration(request.VideoDurationSeconds);
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = identityId;

        // Bump VideoAvatarVersion on a successful rename-and-move so the cache-busting `?v=`
        // query changes — without this, the browser/CDN keeps serving the old URL it cached
        // for the previous name and never refetches under the new key.
        if (nameChanged && hadAvatar)
            entity.VideoAvatarVersion = entity.UpdatedAt.Ticks;
        if (nameChanged && hadKeyframe)
            entity.KeyframeVersion = entity.UpdatedAt.Ticks;

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
                await r2Storage.DeleteAsync(AvatarKey(entity.Name), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to remove R2 avatar for deleted population {Id}; continuing", id);
            }
        }

        if (entity.KeyframeVersion is not null)
        {
            try
            {
                await r2Storage.DeleteAsync(KeyframeKey(entity.Name), cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to remove R2 keyframe for deleted population {Id}; continuing", id);
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
            await r2Storage.UploadAsync(AvatarKey(population.Name), stream, VideoContentType, cancellationToken);
        }

        var now = DateTime.UtcNow;
        population.VideoAvatarVersion = now.Ticks;
        population.UpdatedAt = now;
        population.UpdatedBy = identityId;

        await dbContext.SaveChangesAsync(cancellationToken);
        cache.Remove(ErasCacheKey);
        return (true, null, false);
    }

    public async Task<(bool Success, string? Error, bool NotFound)> UploadKeyframeAsync(
        int id, IFormFile file, string identityId, CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
            return (false, "Uploaded file is empty.", false);
        if (file.Length > MaxKeyframeBytes)
            return (false, $"File exceeds the {MaxKeyframeBytes / (1024 * 1024)} MB limit.", false);
        if (!AllowedKeyframeContentTypes.Contains(file.ContentType))
            return (false, $"Invalid content type '{file.ContentType}'. Allowed: {KeyframeContentType}.", false);

        var population = await dbContext.QpadmPopulations.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (population is null) return (false, null, true);

        await using (var stream = file.OpenReadStream())
        {
            await r2Storage.UploadAsync(KeyframeKey(population.Name), stream, KeyframeContentType, cancellationToken);
        }

        var now = DateTime.UtcNow;
        population.KeyframeVersion = now.Ticks;
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

        await r2Storage.DeleteAsync(AvatarKey(population.Name), cancellationToken);

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
            Url = $"{r2Storage.GetPublicUrl(AvatarKey(r.Name))}?v={r.Version}",
        }).ToList();
    }

    /// <summary>
    /// Bulk-uploads each <c>Data/SeedData/population-videos/{Name}.mp4</c> to R2 under the
    /// slug-derived key <c>qpAdm/population-videos/{slug(Name)}.mp4</c>, then bumps
    /// <see cref="QpadmPopulation.VideoAvatarVersion"/> so clients re-fetch with a fresh
    /// cache-bust query. Both the disk filename and the R2 key derive from the same Name,
    /// which keeps the (name → video) pairing stable across DB reseeds and across environments
    /// that share the R2 bucket.
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
                await r2Storage.UploadAsync(AvatarKey(population.Name), fs, VideoContentType, cancellationToken);
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
