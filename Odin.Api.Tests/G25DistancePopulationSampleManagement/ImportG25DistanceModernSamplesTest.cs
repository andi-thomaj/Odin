using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Odin.Api.Data;
using Odin.Api.Data.Entities;

namespace Odin.Api.Tests.G25DistancePopulationSampleManagement;

public class ImportG25DistanceModernSamplesTest
{
    private const string EraName = "Modern Era (2000–2026 AD)";
    private const string Importer = "data-import-modern";
    private const string SeedFileName = "g25-distance-modern.txt";

    [Fact]
    public async Task Imports_modern_era_population_samples_into_dev_database()
    {
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets(typeof(ApplicationDbContext).Assembly, optional: false)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "DefaultConnection not found. Set it via `dotnet user-secrets` on Odin.Api or the ConnectionStrings__DefaultConnection env var.");

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        await using var db = new ApplicationDbContext(options);

        var era = await db.G25DistanceEras.FirstOrDefaultAsync(e => e.Name == EraName)
            ?? throw new InvalidOperationException($"G25 distance era '{EraName}' not found — run database seeding first.");

        var seedPath = LocateSeedFile();
        Assert.True(File.Exists(seedPath), $"Seed file not found: {seedPath}");

        var existingLabels = await db.G25DistancePopulationSamples
            .Where(x => x.G25DistanceEraId == era.Id)
            .Select(x => x.Label)
            .ToListAsync();
        var existing = new HashSet<string>(existingLabels, StringComparer.Ordinal);

        var now = DateTime.UtcNow;
        var toInsert = new List<G25DistancePopulationSample>();
        var parsed = 0;
        var skipped = 0;

        foreach (var rawLine in await File.ReadAllLinesAsync(seedPath))
        {
            if (string.IsNullOrWhiteSpace(rawLine)) continue;

            var comma = rawLine.IndexOf(',');
            if (comma <= 0) continue;

            var label = rawLine[..comma].Trim();
            var coords = rawLine[(comma + 1)..].Trim();
            if (label.Length == 0 || coords.Length == 0) continue;

            parsed++;

            if (existing.Contains(label))
            {
                skipped++;
                continue;
            }

            toInsert.Add(new G25DistancePopulationSample
            {
                Label = label,
                Coordinates = coords,
                Ids = string.Empty,
                G25DistanceEraId = era.Id,
                CreatedAt = now,
                CreatedBy = Importer,
                UpdatedAt = now,
                UpdatedBy = Importer,
            });
        }

        Assert.True(parsed > 0, "Parsed no rows from seed file — check the file path and contents.");

        if (toInsert.Count == 0)
        {
            return;
        }

        const int batchSize = 500;
        for (var i = 0; i < toInsert.Count; i += batchSize)
        {
            var batch = toInsert.Skip(i).Take(batchSize).ToList();
            db.G25DistancePopulationSamples.AddRange(batch);
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }

        var finalCount = await db.G25DistancePopulationSamples
            .CountAsync(x => x.G25DistanceEraId == era.Id);
        Assert.Equal(existing.Count + toInsert.Count, finalCount);
    }

    private static string LocateSeedFile([CallerFilePath] string? callerFilePath = null)
    {
        // This source file lives at:
        //   Odin/Odin.Api.Tests/G25DistancePopulationSampleManagement/ImportG25DistanceModernSamplesTest.cs
        // The seed file lives at:
        //   Odin/Odin.Api/Data/SeedData/g25-distance-modern.txt
        var testDir = Path.GetDirectoryName(callerFilePath)
            ?? throw new InvalidOperationException("Caller file path unavailable.");

        var candidate = Path.GetFullPath(Path.Combine(
            testDir, "..", "..", "Odin.Api", "Data", "SeedData", SeedFileName));
        if (File.Exists(candidate)) return candidate;

        var baseDir = new DirectoryInfo(AppContext.BaseDirectory);
        while (baseDir is not null)
        {
            var fallback = Path.Combine(baseDir.FullName, "Odin.Api", "Data", "SeedData", SeedFileName);
            if (File.Exists(fallback)) return fallback;
            baseDir = baseDir.Parent;
        }

        return candidate;
    }
}
