using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;

namespace Odin.Api.Endpoints.MediaManagement;

public class MediaService(ApplicationDbContext dbContext) : IMediaService
{
    private static readonly HashSet<string> AllowedAudioContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "audio/wav", "audio/x-wav", "audio/wave", "audio/vnd.wave"
    };

    private static readonly HashSet<string> AllowedVideoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "video/mp4", "video/mpeg", "video/webm"
    };

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

    public async Task<(byte[] Data, string ContentType, string FileName)?> GetPopulationVideoAsync(int populationId)
    {
        var result = await dbContext.PopulationVideoFiles
            .AsNoTracking()
            .Where(f => f.PopulationId == populationId)
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

    public async Task<(bool Success, string? Error)> UploadPopulationVideoAsync(int populationId, IFormFile file, string identityId)
    {
        var population = await dbContext.Populations.FindAsync(populationId);
        if (population is null)
            return (false, $"Population with ID {populationId} not found.");

        if (!AllowedVideoContentTypes.Contains(file.ContentType))
            return (false, $"Invalid content type '{file.ContentType}'. Allowed: video/mp4, video/mpeg, video/webm.");

        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var data = ms.ToArray();
        var now = DateTime.UtcNow;

        var existing = await dbContext.PopulationVideoFiles
            .FirstOrDefaultAsync(f => f.PopulationId == populationId);

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
            dbContext.PopulationVideoFiles.Add(new PopulationVideoFile
            {
                PopulationId = populationId,
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
}
