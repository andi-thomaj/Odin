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
        await SeedG25AdmixtureErasAsync();
    }

    private async Task SeedG25PopulationSamplesAsync()
    {
        // The original seeder kept this guard but returned immediately to skip the
        // bulk import (the file is large and integration tests don't need it).
        // Preserving that behaviour intentionally.
        return;
#pragma warning disable CS0162 // Unreachable code — kept for parity with the original DatabaseSeeder.
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
