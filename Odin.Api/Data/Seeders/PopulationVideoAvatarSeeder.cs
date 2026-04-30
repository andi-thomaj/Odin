using Microsoft.EntityFrameworkCore;
using Odin.Api.Services;

namespace Odin.Api.Data.Seeders;

/// <summary>
/// Seeds the per-population MP4 avatar into <c>QpadmPopulation.VideoAvatarImage</c>
/// by reading the GIF source from <c>Data/SeedData/population-gifs/{PopulationName}.gif</c>
/// (the original artwork is shipped as GIF) and transcoding it once via the
/// shared <see cref="IVideoTranscodeService"/>. Populates any row that currently
/// has no <c>VideoAvatarImage</c> and has a matching GIF on disk; existing
/// non-null values are left untouched so admin uploads aren't overwritten on
/// restart. Skips silently when ffmpeg is unavailable.
/// </summary>
internal sealed class PopulationVideoAvatarSeeder(
    ApplicationDbContext context,
    IVideoTranscodeService transcoder,
    ILogger logger)
{
    public async Task SeedAsync()
    {
        var gifDir = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", "population-gifs");
        if (!Directory.Exists(gifDir))
            return;

        var populations = await context.QpadmPopulations
            .Where(p => p.VideoAvatarImage == null)
            .ToListAsync();

        if (populations.Count == 0)
            return;

        if (!await transcoder.IsAvailableAsync())
        {
            logger.LogWarning(
                "Skipping population video seed: ffmpeg is not available on PATH for the API process.");
            return;
        }

        foreach (var population in populations)
        {
            var filePath = Path.Combine(gifDir, $"{population.Name}.gif");
            if (!File.Exists(filePath)) continue;

            var gifBytes = await File.ReadAllBytesAsync(filePath);
            byte[]? mp4;
            try
            {
                mp4 = await transcoder.GifToMp4Async(gifBytes);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to transcode GIF to MP4 for population {Name} during seed; skipping.",
                    population.Name);
                continue;
            }

            if (mp4 is null) continue;

            population.VideoAvatarImage = mp4;
            await context.SaveChangesAsync();
        }
    }
}
