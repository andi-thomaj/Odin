using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Storage;

namespace Odin.Api.Endpoints.MediaManagement;

public class MediaService(
    ApplicationDbContext dbContext,
    IR2Storage r2Storage,
    ILogger<MediaService> logger) : IMediaService
{
    private const string AudioContentType = "audio/wav";

    private static readonly HashSet<string> AllowedAudioContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/wav", "audio/x-wav", "audio/wave", "audio/vnd.wave"
    };

    /// <summary>R2 object key for a music track's audio. FileName-stable: matches the seed disk filename.
    /// The <c>qpAdm/population-music-tracks/</c> prefix scopes this service's objects inside the
    /// shared <c>ancestrify</c> bucket (sibling of <c>qpAdm/population-videos/</c>).</summary>
    private static string AudioKey(string fileName) => $"qpAdm/population-music-tracks/{fileName}";

    public async Task<(byte[] Data, string ContentType, string FileName)?> GetMusicTrackAudioAsync(int musicTrackId)
    {
        var result = await dbContext.MusicTrackFiles
            .AsNoTracking()
            .Where(f => f.MusicTrackId == musicTrackId)
            .Select(f => new { f.FileData, f.ContentType, f.FileName })
            .FirstOrDefaultAsync();

        if (result is null)
            return null;

        return (result.FileData, result.ContentType, result.FileName);
    }

    public async Task<(bool Success, string? Error)> UploadMusicTrackAudioAsync(int musicTrackId, IFormFile file, string identityId)
    {
        var track = await dbContext.MusicTracks.FindAsync(musicTrackId);
        if (track is null)
            return (false, $"Music track with ID {musicTrackId} not found.");

        if (!AllowedAudioContentTypes.Contains(file.ContentType))
            return (false, $"Invalid content type '{file.ContentType}'. Allowed: audio/wav.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var data = ms.ToArray();
        var now = DateTime.UtcNow;

        var existing = await dbContext.MusicTrackFiles
            .FirstOrDefaultAsync(f => f.MusicTrackId == musicTrackId);

        if (existing is not null)
        {
            existing.FileName = file.FileName;
            existing.FileData = data;
            existing.ContentType = file.ContentType;
            existing.FileSizeBytes = data.Length;
            existing.UpdatedAt = now;
            existing.UpdatedBy = identityId;
        }
        else
        {
            dbContext.MusicTrackFiles.Add(new MusicTrackFile
            {
                MusicTrackId = musicTrackId,
                FileName = file.FileName,
                FileData = data,
                ContentType = file.ContentType,
                FileSizeBytes = data.Length,
                CreatedAt = now,
                CreatedBy = identityId,
                UpdatedAt = now,
                UpdatedBy = identityId,
            });
        }

        await dbContext.SaveChangesAsync();
        return (true, null);
    }

    public async Task<(int Updated, int MissingOnDisk, int Failed, int Unmatched, string? FirstError)> SyncMusicTrackAudioFromDiskAsync(
        string identityId, CancellationToken cancellationToken = default)
    {
        var audioDir = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", "media", "audio");
        if (!Directory.Exists(audioDir))
        {
            logger.LogWarning("Music track sync requested but {Dir} does not exist.", audioDir);
            return (0, 0, 0, 0, $"Audio source directory not found at {audioDir}.");
        }

        logger.LogInformation("Music track audio sync started by {IdentityId}", identityId);

        var tracks = await dbContext.MusicTracks
            .Select(t => new { t.Id, t.FileName })
            .ToListAsync(cancellationToken);

        var updated = 0;
        var missingOnDisk = 0;
        var failed = 0;
        string? firstError = null;

        foreach (var track in tracks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var filePath = Path.Combine(audioDir, track.FileName);
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
                    firstError ??= $"{track.FileName}: file is empty (0 bytes).";
                    continue;
                }
                await r2Storage.UploadAsync(AudioKey(track.FileName), fs, AudioContentType, cancellationToken);
            }
            catch (Exception ex)
            {
                // Error level (not Warning) so the Serilog Postgres sink captures the exception —
                // makes production debugging via the `logs` table possible without shell access.
                logger.LogError(ex,
                    "Failed to upload audio for music track {FileName} (id {Id}) to R2", track.FileName, track.Id);
                failed++;
                // Capture first failure for the API response. Inner-exception message is usually the
                // actionable one for AWS SDK / R2 errors (NoSuchBucket, InvalidAccessKeyId, etc.).
                firstError ??= $"{track.FileName}: {(ex.InnerException?.Message ?? ex.Message)}";
                continue;
            }

            updated++;
        }

        var trackFileNames = new HashSet<string>(tracks.Select(t => t.FileName), StringComparer.Ordinal);
        var unmatched = Directory.EnumerateFiles(audioDir, "*.wav")
            .Select(Path.GetFileName)
            .Count(name => name is not null && !trackFileNames.Contains(name));

        return (updated, missingOnDisk, failed, unmatched, firstError);
    }
}
