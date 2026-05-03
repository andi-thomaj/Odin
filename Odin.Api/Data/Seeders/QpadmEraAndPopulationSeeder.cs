using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data.Entities;

namespace Odin.Api.Data.Seeders;

/// <summary>
/// Seeds the qpAdm era catalog, the music tracks (intro + per-era tracks), and the
/// reference populations (era-grouped, with GeoJSON, color, icon, and music track).
///
/// Population data is loaded from <c>Data/SeedData/qpadm-populations.json</c>
/// (copied to the build output). That JSON is the single source of truth for the
/// per-population fields (Name, Description, GeoJson, IconFileName, Color, EraId,
/// MusicTrackFileName). The MusicTrackFileName joins each population to one of the
/// tracks declared in <c>musicTrackData</c> below — using the file name (which is
/// already stable on-disk) as the key keeps the JSON and code from drifting on
/// human-readable names.
/// </summary>
internal sealed class QpadmEraAndPopulationSeeder(ApplicationDbContext context)
{
    private const string SeederTag = "DatabaseSeeder";

    public async Task SeedAsync()
    {
        if (await context.QpadmEras.AnyAsync())
            return;

        var now = DateTime.UtcNow;

        // ---- Eras ---------------------------------------------------------
        var era1 = new QpadmEra
        {
            Name = "Hunter Gatherer and Neolithic Farmer",
            Description =
                "Encompasses the major ancestral components from the Mesolithic through the Neolithic transition (~12,000–4,000 BCE) identified through ancient DNA as the foundational genetic building blocks of modern Eurasian and global populations.",
            CreatedAt = now,
            CreatedBy = SeederTag,
            UpdatedAt = now,
        };
        var era2 = new QpadmEra
        {
            Name = "Classical Antiquity",
            Description =
                "Represents the historically attested peoples and cultures from the Iron Age through the late Roman and early Medieval periods (~800 BCE–700 CE) whose genetic profiles have been reconstructed through archaeogenomic sampling across the Mediterranean, Balkans, and wider Europe.",
            CreatedAt = now,
            CreatedBy = SeederTag,
            UpdatedAt = now,
        };
        context.QpadmEras.Add(era1);
        context.QpadmEras.Add(era2);

        var erasById = new Dictionary<int, QpadmEra> { [1] = era1, [2] = era2 };

        // ---- Music tracks -------------------------------------------------
        // Intro (DisplayOrder 0, not linked to a population).
        var introTrack = new MusicTrack
        {
            Name = "Intro",
            FileName = "intro.wav",
            DisplayOrder = 0,
            CreatedAt = now,
            CreatedBy = SeederTag,
            UpdatedAt = now,
        };
        context.MusicTracks.Add(introTrack);

        // (DisplayOrder, Name, FileName). Populations join to a track via the
        // population's MusicTrackFileName field in qpadm-populations.json — using the
        // file name as the key avoids the prior brittle name-based join, where any
        // whitespace/spelling drift between this list and the JSON's display name
        // would throw at startup.
        var musicTrackData = new (int Order, string Name, string FileName)[]
        {
            // Era 1 — Hunter Gatherer and Neolithic Farmer
            (1, "European Foragers", "european-foragers.wav"),
            (2, "Eurasian Steppe", "eurasian-steppe.wav"),
            (3, "Near Eastern / Anatolian Farmers", "near-eastern-farmers.wav"),
            (4, "Levantine & North African", "levantine-north-african.wav"),
            (5, "East Asian & Native American", "east-asian-native-american.wav"),
            (6, "Sub-Saharan African", "sub-saharan-african.wav"),

            // Era 2 — Classical Antiquity
            (7, "Hellenic", "hellenic.wav"),
            (8, "Roman / Italic", "roman-italic.wav"),
            (9, "Balkan / Paleo-Balkan", "balkan-paleo-balkan.wav"),
            (10, "Anatolian / Caucasian", "anatolian-caucasian.wav"),
            (11, "Semitic / Phoenician", "semitic-phoenician.wav"),
            (12, "Celtic", "celtic.wav"),
            (13, "Western Mediterranean", "western-mediterranean-pre-ie.wav"),
            (14, "Germanic", "germanic-sarmatian.wav"),
            (15, "Medieval Slavic", "medieval-slavic.wav"),

            // Standalone tracks (not yet linked to seeded populations)
            (16, "Central Asian Nomadic", "central-asian-nomadic.wav"),
            (17, "North African Amazigh", "north-african-amazigh.wav"),
        };

        var tracksByFileName = new Dictionary<string, MusicTrack>(StringComparer.Ordinal);
        foreach (var (order, name, fileName) in musicTrackData)
        {
            var track = new MusicTrack
            {
                Name = name,
                FileName = fileName,
                DisplayOrder = order,
                CreatedAt = now,
                CreatedBy = SeederTag,
                UpdatedAt = now,
            };
            context.MusicTracks.Add(track);
            tracksByFileName[fileName] = track;
        }

        // ---- Populations (loaded from JSON) -------------------------------
        var populationsSeed = LoadPopulationsSeed();
        foreach (var seed in populationsSeed)
        {
            if (!erasById.TryGetValue(seed.EraId, out var era))
                throw new InvalidOperationException(
                    $"Population '{seed.Name}' references unknown EraId {seed.EraId}.");
            if (string.IsNullOrWhiteSpace(seed.MusicTrackFileName))
                throw new InvalidOperationException(
                    $"Population '{seed.Name}' has no MusicTrackFileName set in qpadm-populations.json.");
            if (!tracksByFileName.TryGetValue(seed.MusicTrackFileName, out var track))
                throw new InvalidOperationException(
                    $"Population '{seed.Name}' references unknown music track file name '{seed.MusicTrackFileName}'. " +
                    "Add it to musicTrackData or fix qpadm-populations.json.");

            context.QpadmPopulations.Add(new QpadmPopulation
            {
                Name = seed.Name,
                Description = seed.Description,
                Era = era,
                GeoJson = seed.GeoJson,
                IconFileName = seed.IconFileName,
                Color = seed.Color,
                MusicTrack = track,
                CreatedAt = now,
                CreatedBy = SeederTag,
                UpdatedAt = now,
            });
        }

        await context.SaveChangesAsync();
    }

    private static IReadOnlyList<PopulationSeed> LoadPopulationsSeed()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", "qpadm-populations.json");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"Population seed file not found at '{path}'. Make sure Data/SeedData/qpadm-populations.json is set to copy to the build output.");

        var json = File.ReadAllText(path);
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var list = JsonSerializer.Deserialize<List<PopulationSeed>>(json, options);
        if (list is null || list.Count == 0)
            throw new InvalidOperationException(
                "qpadm-populations.json deserialised to an empty list — check the file contents.");
        return list;
    }

    private sealed record PopulationSeed(
        string Name,
        string Description,
        string GeoJson,
        string IconFileName,
        string Color,
        int EraId,
        string MusicTrackFileName);
}
