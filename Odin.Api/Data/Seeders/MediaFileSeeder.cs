using Microsoft.EntityFrameworkCore;
using Odin.Api.Data.Entities;

namespace Odin.Api.Data.Seeders;

/// <summary>
/// Seeds binary audio files into <see cref="MusicTrackFile"/>. Reads from
/// <c>Data/SeedData/media/audio/{trackFileName}</c> next to the running
/// assembly. No-op when the table already contains rows or the source
/// directory doesn't exist. Each file is saved in its own SaveChanges call so
/// the per-row work stays inside the configured DB command timeout.
/// </summary>
internal sealed class MediaFileSeeder(ApplicationDbContext context)
{
    private const string SeederTag = "DatabaseSeeder";

    public async Task SeedAsync()
    {
        if (await context.MusicTrackFiles.AnyAsync())
            return;

        var mediaRoot = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", "media");
        if (!Directory.Exists(mediaRoot))
            return;

        var audioDir = Path.Combine(mediaRoot, "audio");
        if (!Directory.Exists(audioDir))
            return;

        var now = DateTime.UtcNow;
        var tracks = await context.MusicTracks.ToListAsync();
        foreach (var track in tracks)
        {
            var filePath = Path.Combine(audioDir, track.FileName);
            if (!File.Exists(filePath)) continue;

            var data = await File.ReadAllBytesAsync(filePath);
            context.MusicTrackFiles.Add(new MusicTrackFile
            {
                MusicTrackId = track.Id,
                FileName = track.FileName,
                FileData = data,
                ContentType = "audio/wav",
                FileSizeBytes = data.Length,
                CreatedAt = now,
                CreatedBy = SeederTag,
                UpdatedAt = now,
            });
            await context.SaveChangesAsync();
        }
    }
}
