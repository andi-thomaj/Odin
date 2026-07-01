using System.Globalization;
using System.Text.Json;

namespace Odin.Api.Tests.SeedDataExport;

/// <summary>
/// Validates the committed G25 PCA population-sample seed (produced offline by
/// <c>tools/GenerateG25PcaSeed</c>). These are structural guards so a bad regeneration can't ship a
/// seed the startup seeder / PCA parser would choke on: every row must carry exactly 25 PC values, a
/// known era id (1-6, the G25DistanceEra catalog), and a non-empty label — and every (era, label, ids)
/// triple must be unique (the seeder inserts one row per triple).
/// </summary>
public class G25PcaPopulationSamplesSeedTests
{
    private const int ExpectedCoordinateCount = 25;
    private static readonly int[] ValidEraIds = [1, 2, 3, 4, 5, 6];
    private const int ModernEraId = 6;

    [Fact]
    public void PcaSeed_IsWellFormed_ForEveryRow()
    {
        var samples = LoadSeed();

        // Sanity bounds — the ancient ids + all Modern individuals land in the tens of thousands. Loose
        // enough to survive a re-generation against refreshed coordinate files.
        Assert.InRange(samples.Count, 15_000, 40_000);

        foreach (var sample in samples)
        {
            Assert.False(string.IsNullOrWhiteSpace(sample.SampleLabel), "PCA sample has an empty label.");
            Assert.NotNull(sample.Coordinates);
            Assert.Contains(sample.G25DistanceEraId, ValidEraIds);

            var parts = sample.Coordinates!.Split(',');
            Assert.True(
                parts.Length == ExpectedCoordinateCount,
                $"'{sample.SampleLabel}' (era {sample.G25DistanceEraId}) has {parts.Length} coordinate " +
                $"values, expected {ExpectedCoordinateCount}.");

            foreach (var part in parts)
                Assert.True(
                    double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
                    $"'{sample.SampleLabel}' has a non-numeric coordinate value '{part}'.");
        }
    }

    [Fact]
    public void PcaSeed_CoversEveryEra_AndModernIsLargest()
    {
        var samples = LoadSeed();
        var perEra = samples
            .GroupBy(s => s.G25DistanceEraId)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var eraId in ValidEraIds)
            Assert.True(perEra.ContainsKey(eraId) && perEra[eraId] > 0, $"Era {eraId} has no PCA samples.");

        // Modern (all file rows) is by far the biggest cluster of samples.
        var modern = perEra[ModernEraId];
        Assert.True(
            ValidEraIds.Where(e => e != ModernEraId).All(e => perEra[e] < modern),
            "Modern era should hold more PCA samples than any single ancient era.");
    }

    [Fact]
    public void PcaSeed_HasNoDuplicateEraLabelIdsTriples()
    {
        var samples = LoadSeed();
        var seen = new HashSet<string>();
        foreach (var s in samples)
        {
            var key = $"{s.G25DistanceEraId}\n{s.SampleLabel}\n{s.Ids}";
            Assert.True(seen.Add(key), $"Duplicate PCA seed row for triple: {key.Replace('\n', '|')}");
        }
    }

    private static List<SampleRow> LoadSeed()
    {
        var json = File.ReadAllText(ResolveSeedFilePath());
        var samples = JsonSerializer.Deserialize<List<SampleRow>>(
            json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        Assert.NotNull(samples);
        Assert.NotEmpty(samples!);
        return samples!;
    }

    private static string ResolveSeedFilePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Odin.slnx")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException(
                "Could not locate Odin.slnx — unable to resolve the seed-data file path.");

        return Path.Combine(dir.FullName, "Odin.Api", "Data", "SeedData", "g25_pca_population_samples.json");
    }

    private sealed record SampleRow(
        string SampleLabel,
        string? Coordinates,
        string Ids,
        int G25DistanceEraId);
}
