using Microsoft.EntityFrameworkCore;

namespace Odin.Api.Data.Seeders;

/// <summary>
/// Copies each <c>Data/SeedData/population-videos/{PopulationName}.mp4</c> on disk
/// into <c>QpadmPopulation.VideoAvatarImage</c> verbatim. The MP4 file on disk is
/// the final-quality video served to the frontend — no transcoding happens at
/// seed time, so the bytes that land in the database are exactly the bytes the
/// frontend plays. Populates any row that currently has no <c>VideoAvatarImage</c>
/// and has a matching MP4 on disk; existing non-null values are left untouched
/// so admin uploads aren't overwritten on restart.
/// </summary>
internal sealed class PopulationVideoAvatarSeeder(
    ApplicationDbContext context,
    ILogger logger)
{
    public async Task SeedAsync()
    {
        var videoDir = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", "population-videos");
        if (!Directory.Exists(videoDir))
        {
            logger.LogInformation(
                "Skipping population video seed: {Dir} does not exist.", videoDir);
            return;
        }

        var populations = await context.QpadmPopulations
            .Where(p => p.VideoAvatarImage == null)
            .ToListAsync();

        if (populations.Count == 0)
            return;

        foreach (var population in populations)
        {
            var filePath = Path.Combine(videoDir, $"{population.Name}.mp4");
            if (!File.Exists(filePath)) continue;

            byte[] mp4;
            try
            {
                mp4 = await File.ReadAllBytesAsync(filePath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to read MP4 file for population {Name} during seed; skipping.",
                    population.Name);
                continue;
            }

            if (mp4.Length == 0) continue;

            population.VideoAvatarImage = mp4;
            await context.SaveChangesAsync();
        }
    }
}
