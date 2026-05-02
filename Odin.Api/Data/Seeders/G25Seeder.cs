using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;

namespace Odin.Api.Data.Seeders;

/// <summary>
/// Seeds the G25 reference data: admixture population samples, G25 eras (distance
/// + admixture), and the G25 service entity tree (continents → ethnicities). Each
/// sub-step is independently idempotent so individual rows can be added without
/// re-seeding the rest. Catalog/commerce data (products, prices, addons) is
/// owned by Paddle and synced into <c>paddle_products</c>/<c>paddle_prices</c>;
/// this seeder no longer touches it.
/// </summary>
internal sealed class G25Seeder(ApplicationDbContext context)
{
    private const string SeederTag = "DatabaseSeeder";

    public async Task SeedAsync()
    {
        await SeedG25PopulationSamplesAsync();
        await SeedG25ServiceAsync();
        await SeedG25DistanceErasAsync();
        await SeedG25DistancePopulationSamplesAsync();
        await SeedG25AdmixtureErasAsync();
    }

    private async Task SeedG25PopulationSamplesAsync()
    {
        if (await context.G25AdmixturePopulationSamples.AnyAsync())
            return;

        var path = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", "g25-ancients.txt");
        if (!File.Exists(path))
            return;

        var now = DateTime.UtcNow;
        const int batchSize = 1000;

        var lines = await File.ReadAllLinesAsync(path);
        var batch = new List<G25AdmixturePopulationSample>();

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var idx = line.IndexOf(':');
            if (idx <= 0 || idx >= line.Length - 1)
                continue;

            var label = line[..idx].Trim();
            var coords = line[(idx + 1)..].Trim();
            if (label.Length == 0 || coords.Length == 0)
                continue;

            batch.Add(new G25AdmixturePopulationSample
            {
                Label = label,
                Coordinates = coords,
                CreatedAt = now,
                CreatedBy = SeederTag,
                UpdatedAt = now,
                UpdatedBy = SeederTag
            });

            if (batch.Count < batchSize)
                continue;

            context.G25AdmixturePopulationSamples.AddRange(batch);
            await context.SaveChangesAsync();
            batch.Clear();
        }

        if (batch.Count > 0)
        {
            context.G25AdmixturePopulationSamples.AddRange(batch);
            await context.SaveChangesAsync();
        }
#pragma warning restore CS0162
    }

    private async Task SeedG25ServiceAsync()
    {
        if (await context.G25Ethnicities.AnyAsync())
            return;

        var now = DateTime.UtcNow;

        var europe = new G25Continent { Name = "Europe", CreatedBy = "seed", CreatedAt = now, UpdatedAt = now };
        context.G25Continents.Add(europe);
        await context.SaveChangesAsync();

        var ethnicities = new[]
        {
            new G25Ethnicity { Name = "Albanian", G25ContinentId = europe.Id, CreatedBy = "seed", CreatedAt = now, UpdatedAt = now },
            new G25Ethnicity { Name = "Greek", G25ContinentId = europe.Id, CreatedBy = "seed", CreatedAt = now, UpdatedAt = now },
            new G25Ethnicity { Name = "Italian", G25ContinentId = europe.Id, CreatedBy = "seed", CreatedAt = now, UpdatedAt = now },
        };
        context.G25Ethnicities.AddRange(ethnicities);
        await context.SaveChangesAsync();
    }

    private async Task SeedG25DistanceErasAsync()
    {
        if (await context.G25DistanceEras.AnyAsync())
            return;

        var now = DateTime.UtcNow;

        var eraNames = new[]
        {
            "Late Bronze Age (3000–1200 BC)",
            "Pre-Classical Iron Age (1200–0 BC)",
            "Imperial Antiquity (0–600 AD)",
            "Middle Ages (600–1400 AD)",
            "Early Modern Period (1400–2000 AD)",
            "Modern Era (2000–2026 AD)",
        };

        foreach (var name in eraNames)
        {
            context.G25DistanceEras.Add(new G25DistanceEra
            {
                Name = name,
                CreatedAt = now,
                CreatedBy = SeederTag,
                UpdatedAt = now,
                UpdatedBy = SeederTag,
            });
        }

        await context.SaveChangesAsync();
    }

    private async Task SeedG25DistancePopulationSamplesAsync()
    {
        if (await context.G25DistancePopulationSamples.AnyAsync())
            return;

        var path = Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", "g25_distance_population_samples.json");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"G25 distance population sample seed file not found at '{path}'. " +
                "Make sure Data/SeedData/g25_distance_population_samples.json is set to copy to the build output.");

        var seeds = JsonSerializer.Deserialize<List<DistancePopulationSampleSeed>>(
            await File.ReadAllTextAsync(path),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        if (seeds is null || seeds.Count == 0)
            throw new InvalidOperationException(
                "g25_distance_population_samples.json deserialised to an empty list — check the file contents.");

        var validEraIds = await context.G25DistanceEras.Select(e => e.Id).ToHashSetAsync();
        var now = DateTime.UtcNow;
        const int batchSize = 1000;
        var batch = new List<G25DistancePopulationSample>(batchSize);

        foreach (var seed in seeds)
        {
            if (string.IsNullOrWhiteSpace(seed.Label) || string.IsNullOrWhiteSpace(seed.Coordinates))
                continue;
            if (!validEraIds.Contains(seed.G25DistanceEraId))
                throw new InvalidOperationException(
                    $"Population sample '{seed.Label}' references unknown G25DistanceEraId {seed.G25DistanceEraId}. " +
                    "Re-check seed data against the G25 distance era catalog.");

            batch.Add(new G25DistancePopulationSample
            {
                Label = seed.Label,
                Coordinates = seed.Coordinates,
                Ids = seed.Ids ?? string.Empty,
                G25DistanceEraId = seed.G25DistanceEraId,
                CreatedAt = now,
                CreatedBy = SeederTag,
                UpdatedAt = now,
                UpdatedBy = SeederTag,
            });

            if (batch.Count < batchSize)
                continue;

            context.G25DistancePopulationSamples.AddRange(batch);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            batch.Clear();
        }

        if (batch.Count > 0)
        {
            context.G25DistancePopulationSamples.AddRange(batch);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
        }
    }

    private sealed record DistancePopulationSampleSeed(
        string Label,
        string Coordinates,
        string? Ids,
        int G25DistanceEraId);

    private async Task SeedG25AdmixtureErasAsync()
    {
        if (await context.G25AdmixtureEras.AnyAsync())
            return;

        var now = DateTime.UtcNow;

        var eraNames = new[]
        {
            "Bronze & Iron Age (2000–0 BC)",
            "Imperial Antiquity (0–600 AD)",
            "Middle Ages (600–1400 AD)",
            "Early Modern (1400–1900 AD)",
        };

        foreach (var name in eraNames)
        {
            context.G25AdmixtureEras.Add(new G25AdmixtureEra
            {
                Name = name,
                CreatedAt = now,
                CreatedBy = SeederTag,
                UpdatedAt = now,
                UpdatedBy = SeederTag,
            });
        }

        await context.SaveChangesAsync();
    }

}
