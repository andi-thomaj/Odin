using System.Text;
using System.Text.Json;
using Odin.Api.Data.Seeders;

// G25 PCA seed generator.
//
// Derives the PER-POPULATION G25 PCA population samples (one cluster row per population, carrying all its
// member individuals' coordinates as ';'-joined 25-value groups) from (a) the already-seeded G25 distance
// population samples and (b) the per-era G25 coordinate files, and writes
// Data/SeedData/g25_pca_population_samples.json (same record shape the distance seed uses) plus a
// human-readable Data/SeedData/g25_pca_seed_report.txt. The matching rules live in the shared
// G25PcaSeedBuilder (Odin.Api) so this tool and the G25PcaPopulationSamplesImportTests agree.
//
// Usage (run from the Odin/ directory). Both paths default to the in-repo locations, so no args are
// needed after the coordinate files were committed under Data/SeedData/g25-coordinates:
//   dotnet run --project tools/GenerateG25PcaSeed
//   dotnet run --project tools/GenerateG25PcaSeed -- --genetic-dir "<folder with the 6 era .txt files>"

var argMap = ParseArgs(args);

var geneticDir = argMap.GetValueOrDefault(
    "genetic-dir", Path.Combine("Odin.Api", "Data", "SeedData", "g25-coordinates"));
var distanceJson = argMap.GetValueOrDefault(
    "distance-json", Path.Combine("Odin.Api", "Data", "SeedData", "g25_distance_population_samples.json"));
var outPath = argMap.GetValueOrDefault(
    "out", Path.Combine("Odin.Api", "Data", "SeedData", "g25_pca_population_samples.json"));
var reportPath = argMap.GetValueOrDefault(
    "report", Path.Combine("Odin.Api", "Data", "SeedData", "g25_pca_seed_report.txt"));

if (!File.Exists(distanceJson))
{
    Console.Error.WriteLine($"Distance seed file not found: '{distanceJson}'.");
    return 1;
}

// ---- Load per-era coordinate file rows ----
var eraRows = new Dictionary<int, List<G25CoordinateFileRow>>();
foreach (var (eraId, fileName) in G25PcaSeedBuilder.EraFileNames)
{
    var path = Path.Combine(geneticDir, fileName);
    if (!File.Exists(path))
    {
        Console.Error.WriteLine($"Era {eraId} coordinate file not found: '{path}'.");
        return 1;
    }

    eraRows[eraId] = G25PcaSeedBuilder.ParseFileRows(File.ReadAllLines(path));
    Console.WriteLine($"Era {eraId}: read {eraRows[eraId].Count} coordinate rows from '{fileName}'.");
}

// ---- Load distance seeds ----
var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
var distanceSeeds = JsonSerializer.Deserialize<List<DistanceSeed>>(
    await File.ReadAllTextAsync(distanceJson), jsonOpts) ?? new List<DistanceSeed>();
var distanceSamples = distanceSeeds
    .Select(s => new G25PcaDistanceSample(s.SampleLabel, s.Ids, s.G25DistanceEraId))
    .ToList();

// ---- Derive PCA records (shared logic) ----
var result = G25PcaSeedBuilder.Build(distanceSamples, eraRows);

// ---- Write output JSON (indented, matching the distance seed's formatting) ----
var outDir = Path.GetDirectoryName(outPath);
if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
var outputRecords = result.Records
    .Select(r => new PcaSeedRecord(r.Label, r.Coordinates, r.Ids, r.EraId, null))
    .ToList();
await File.WriteAllTextAsync(
    outPath, JsonSerializer.Serialize(outputRecords, new JsonSerializerOptions { WriteIndented = true }));

// ---- Write the report ----
var report = new StringBuilder();
report.AppendLine("G25 PCA seed generation report");
report.AppendLine($"Generated (UTC): {DateTime.UtcNow:O}");
report.AppendLine($"Distance seed:   {distanceJson}");
report.AppendLine($"Genetic dir:     {geneticDir}");
report.AppendLine($"Output JSON:     {outPath}");
report.AppendLine(
    $"PCA clusters (one row per population): {result.Records.Count}  " +
    $"(ancient {result.AncientClusters} + modern {result.ModernClusters})");
report.AppendLine(
    $"Member individuals aggregated:        {result.AncientMembers + result.ModernMembers}  " +
    $"(ancient {result.AncientMembers} + modern {result.ModernMembers})");
report.AppendLine($"Dedup skipped (duplicate members):    {result.DedupSkipped}");
report.AppendLine();
report.AppendLine($"Unmatched ids (absent from the era file): {result.Unmatched.Count}");
foreach (var u in result.Unmatched)
    report.AppendLine($"  [era {u.EraId}] {u.SampleLabel} :: {u.Token}");
report.AppendLine();
report.AppendLine($"Dirty tokens skipped (coordinate noise / mangled): {result.Dirty.Count}");
foreach (var d in result.Dirty)
    report.AppendLine($"  [era {d.EraId}] {d.SampleLabel} :: {d.Token}");

await File.WriteAllTextAsync(reportPath, report.ToString());

Console.WriteLine();
Console.WriteLine($"Wrote {result.Records.Count} PCA cluster records -> {outPath}");
Console.WriteLine($"  clusters: ancient {result.AncientClusters} + modern {result.ModernClusters}; " +
                  $"members: ancient {result.AncientMembers} + modern {result.ModernMembers}; " +
                  $"unmatched {result.Unmatched.Count}; dirty {result.Dirty.Count}; dedup skipped {result.DedupSkipped}");
Console.WriteLine($"Report -> {reportPath}");
return 0;

// ---------- helpers ----------

static Dictionary<string, string> ParseArgs(string[] args)
{
    var map = new Dictionary<string, string>(StringComparer.Ordinal);
    for (var i = 0; i < args.Length; i++)
    {
        if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
        var key = args[i][2..];
        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
        {
            map[key] = args[i + 1];
            i++;
        }
        else
        {
            map[key] = "true";
        }
    }

    return map;
}

internal sealed record DistanceSeed(string SampleLabel, string? Coordinates, string? Ids, int G25DistanceEraId);

internal sealed record PcaSeedRecord(
    string SampleLabel, string Coordinates, string Ids, int G25DistanceEraId, object? ResearchLinks);
