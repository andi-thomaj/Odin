using System.Globalization;
using System.Text.Json;

namespace Odin.Api.Tests.SeedDataExport;

/// <summary>
/// Validates the committed G25 PCA population-sample seed (produced offline by
/// <c>tools/GenerateG25PcaSeed</c>). The PCA dataset now holds ONE row per POPULATION per era (a
/// cluster): its <c>Coordinates</c> is the member individuals' 25-value groups joined with ';'. These
/// structural guards keep a bad regeneration from shipping a seed the startup seeder / PCA parser would
/// choke on: every member group is exactly 25 numeric PC values, every era id is known (1-6), every
/// label non-empty, and <c>(era, label)</c> is unique (one cluster per population per era).
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

        // One row per (era, population). Loose bounds so a regeneration against refreshed coordinate
        // files survives, while still catching a collapse to ~0 or a regression back to per-individual
        // (which would land in the tens of thousands).
        Assert.InRange(samples.Count, 100, 10_000);

        foreach (var sample in samples)
        {
            Assert.False(string.IsNullOrWhiteSpace(sample.SampleLabel), "PCA sample has an empty label.");
            Assert.NotNull(sample.Coordinates);
            Assert.Contains(sample.G25DistanceEraId, ValidEraIds);

            var groups = MemberGroups(sample.Coordinates!);
            Assert.True(
                groups.Count >= 1,
                $"'{sample.SampleLabel}' (era {sample.G25DistanceEraId}) has no member coordinate groups.");

            foreach (var group in groups)
            {
                var parts = group.Split(',');
                Assert.True(
                    parts.Length == ExpectedCoordinateCount,
                    $"'{sample.SampleLabel}' (era {sample.G25DistanceEraId}) has a member group with " +
                    $"{parts.Length} values, expected {ExpectedCoordinateCount}.");

                foreach (var part in parts)
                    Assert.True(
                        double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out _),
                        $"'{sample.SampleLabel}' has a non-numeric coordinate value '{part}'.");
            }
        }
    }

    [Fact]
    public void PcaSeed_CoversEveryEra_AndModernHasMostMembers()
    {
        var samples = LoadSeed();

        foreach (var eraId in ValidEraIds)
            Assert.True(samples.Any(s => s.G25DistanceEraId == eraId), $"Era {eraId} has no PCA samples.");

        // "Largest" is now measured by aggregated MEMBER individuals (the point cloud), not row count —
        // one row per population means the row count tracks population diversity, not cloud size.
        var membersPerEra = ValidEraIds.ToDictionary(
            era => era,
            era => samples.Where(s => s.G25DistanceEraId == era).Sum(s => MemberGroups(s.Coordinates!).Count));

        var modernMembers = membersPerEra[ModernEraId];
        Assert.True(
            ValidEraIds.Where(e => e != ModernEraId).All(e => membersPerEra[e] < modernMembers),
            "Modern era should aggregate more member individuals than any single ancient era.");
    }

    [Fact]
    public void PcaSeed_HasUniqueEraLabel()
    {
        var samples = LoadSeed();
        var seen = new HashSet<string>();
        foreach (var s in samples)
        {
            var key = $"{s.G25DistanceEraId}\n{s.SampleLabel}";
            Assert.True(seen.Add(key), $"Duplicate PCA cluster for (era, label): {key.Replace('\n', '|')}");
        }
    }

    private static List<string> MemberGroups(string coordinates) =>
        coordinates
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

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
