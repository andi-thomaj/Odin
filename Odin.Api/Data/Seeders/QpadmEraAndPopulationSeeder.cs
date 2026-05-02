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
/// per-population fields (Name, Description, GeoJson, IconFileName, Color, EraId).
/// Era definitions and music-track wiring stay in code because they describe how
/// the populations group together for the Ancient Origins experience.
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

        // (DisplayOrder, Name, FileName, Population names assigned to this track).
        var musicTrackData = new (int Order, string Name, string FileName, string[] Populations)[]
        {
            // Era 1 — Hunter Gatherer and Neolithic Farmer
            (1, "European Foragers", "european-foragers.wav",
                ["Western Hunter Gatherer (12000 - 6000 BC)", "Eastern Hunter Gatherer (12000 - 5000 BC)",
                 "Baltic (BC 200 - 600 AD)", "Finno-Ugric Volga (0 - 400 AD)", "Saami (0 - 700 AD)"]),
            (2, "Eurasian Steppe", "eurasian-steppe.wav",
                ["Western Steppe Herder (5000 - 2800 BC)"]),
            (3, "Near Eastern / Anatolian Farmers", "near-eastern-farmers.wav",
                ["Anatolian Neolithic Farmer (8500 - 6000 BC)", "Iranian Neolithic Farmer (8000 - 5000 BC)",
                 "Caucasian Hunter Gatherer (13000 - 7000 BC)", "Ancestral South Indian (10,000 - 2000 BC)"]),
            (4, "Levantine & North African", "levantine-north-african.wav",
                ["Natufian (12,000 - 8000 BC)", "North African Farmer (5200 - 4000 BC)"]),
            (5, "East Asian & Native American", "east-asian-native-american.wav",
                ["Northeast Asian (6000 - 2000 BC)", "Native American (15000 - 1500 BC)"]),
            (6, "Sub-Saharan African", "sub-saharan-african.wav",
                ["Sub Saharan Africans"]),

            // Era 2 — Classical Antiquity
            (7, "Hellenic", "hellenic.wav",
                ["Ancient Greek (1500 - 300 BC)", "Pontic (300 - 50 BC)",
                 "East Mediterranean (0 - 600 AD)", "West Anatolia (0 - 600 AD)",
                 "Aegean (BC 200 - 600 AD)"]),
            (8, "Roman / Italic", "roman-italic.wav",
                ["Latin and Etruscan (850 - 150 BC)", "Moesia Superior (0 - 500 AD)",
                 "North Africa (0 - 500 AD)", "Imperial Italy (0 - 550 AD)"]),
            (9, "Balkan / Paleo-Balkan", "balkan-paleo-balkan.wav",
                ["Illyrian (1200 - 250 BC)", "Thracian (850 - 0 BC)",
                 "Proto Albanian (500 - 900 AD)"]),
            (10, "Anatolian / Caucasian", "anatolian-caucasian.wav",
                ["Anatolian (1800 - 300 BC)"]),
            (11, "Semitic / Phoenician", "semitic-phoenician.wav",
                ["Phoenician (850 - 50 BC)", "Carthaginian (800 - 150 BC)"]),
            (12, "Celtic", "celtic.wav",
                ["Celtic (600 - 50 BC)"]),
            (13, "Western Mediterranean", "western-mediterranean-pre-ie.wav",
                ["Iberian (800 - 50 BC)"]),
            (14, "Germanic", "germanic-sarmatian.wav",
                ["Germanic (0 - 500 AD)"]),
            (15, "Medieval Slavic", "medieval-slavic.wav",
                ["Early Slavic (600 - 1200 AD)"]),

            // Standalone tracks (not yet linked to seeded populations)
            (16, "Central Asian Nomadic", "central-asian-nomadic.wav", Array.Empty<string>()),
            (17, "North African Amazigh", "north-african-amazigh.wav", Array.Empty<string>()),
        };

        var popToTrack = new Dictionary<string, MusicTrack>(StringComparer.Ordinal);
        foreach (var (order, name, fileName, populationNames) in musicTrackData)
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

            foreach (var popName in populationNames)
                popToTrack[popName] = track;
        }

        // ---- Populations (loaded from JSON) -------------------------------
        var populationsSeed = LoadPopulationsSeed();
        foreach (var seed in populationsSeed)
        {
            if (!erasById.TryGetValue(seed.EraId, out var era))
                throw new InvalidOperationException(
                    $"Population '{seed.Name}' references unknown EraId {seed.EraId}.");
            if (!popToTrack.TryGetValue(seed.Name, out var track))
                throw new InvalidOperationException(
                    $"Population '{seed.Name}' is not assigned to any music track. Update the seeder's musicTrackData mapping.");

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
        int EraId);
}
