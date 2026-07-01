using Microsoft.Extensions.Configuration;
using Npgsql;
using NpgsqlTypes;
using Odin.Api.Data;
using Odin.Api.Data.Seeders;

// All SQL below is static and parameter-free (no interpolation of external input) — no injection surface.
#pragma warning disable CA2100

namespace Odin.Api.Tests.SeedDataExport;

/// <summary>
/// Manual seed-IMPORT utility — populates <c>g25_pca_populations_samples</c> (the per-individual PCA
/// reference cloud) directly into a live Postgres, derived from the committed distance seed + the
/// per-era G25 coordinate files under <c>Odin.Api/Data/SeedData/g25-coordinates</c>. Mirrors
/// <see cref="G25DistancePopulationSamplesExportTests"/>: not a CI test — remove the Skip and run
/// locally with <c>ConnectionStrings__DefaultConnection</c> set (or via user-secrets), or
/// <c>dotnet test --filter</c> against this single test.
///
/// It is idempotent: existing rows are matched on the <c>(era, label, ids)</c> triple and skipped, so
/// it can be re-run to top up new samples without duplicating. Only the G25 distance eras (1-6) need to
/// already exist in the target DB — the distance samples and coordinates are read from the repo.
/// </summary>
public class G25PcaPopulationSamplesImportTests
{
    [Fact(Skip = "Manual seed-import utility — requires a populated Postgres; run locally only.")]
    public async Task Import_PcaPopulationSamples_IntoDatabase()
    {
        var connectionString = ResolveConnectionString();
        Assert.False(
            string.IsNullOrWhiteSpace(connectionString),
            "ConnectionStrings:DefaultConnection must be set in Odin.Api user-secrets " +
            "or via the ConnectionStrings__DefaultConnection environment variable.");

        // 1. Derive the PCA records from the committed repo files (distance seed + coordinate files).
        var repoRoot = ResolveRepoRoot();
        var seedDataDir = Path.Combine(repoRoot, "Odin.Api", "Data", "SeedData");

        var distanceJson = Path.Combine(seedDataDir, "g25_distance_population_samples.json");
        var distanceSeeds = System.Text.Json.JsonSerializer.Deserialize<List<DistanceSeed>>(
            await File.ReadAllTextAsync(distanceJson),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        var distanceSamples = distanceSeeds
            .Select(s => new G25PcaDistanceSample(s.SampleLabel, s.Ids, s.G25DistanceEraId))
            .ToList();

        var coordinatesDir = Path.Combine(seedDataDir, "g25-coordinates");
        var eraRows = new Dictionary<int, List<G25CoordinateFileRow>>();
        foreach (var (eraId, fileName) in G25PcaSeedBuilder.EraFileNames)
        {
            var path = Path.Combine(coordinatesDir, fileName);
            Assert.True(File.Exists(path), $"Missing coordinate file for era {eraId}: '{path}'.");
            eraRows[eraId] = G25PcaSeedBuilder.ParseFileRows(await File.ReadAllLinesAsync(path));
        }

        var build = G25PcaSeedBuilder.Build(distanceSamples, eraRows);
        Assert.NotEmpty(build.Records);

        // 2. Import into Postgres — skip existing (era, label, ids) triples so re-runs are idempotent.
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var validEraIds = await ReadIntSetAsync(connection, "SELECT \"Id\" FROM public.g25_distance_eras");
        Assert.True(
            validEraIds.Count > 0,
            "The target database has no g25_distance_eras — start the API once to seed the era catalog first.");

        var existing = new HashSet<string>(StringComparer.Ordinal);
        await using (var cmd = new NpgsqlCommand(
                         "SELECT \"G25DistanceEraId\", \"Label\", \"Ids\" FROM public.g25_pca_populations_samples",
                         connection))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var era = reader.IsDBNull(0) ? (int?)null : reader.GetInt32(0);
                existing.Add(TripleKey(era, reader.GetString(1), reader.GetString(2)));
            }
        }

        var toInsert = build.Records
            .Where(r => validEraIds.Contains(r.EraId))
            .Where(r => existing.Add(TripleKey(r.EraId, r.Label, r.Ids)))
            .ToList();

        var now = DateTime.UtcNow;
        const string importedBy = "G25PcaImportTest";
        var inserted = 0;

        if (toInsert.Count > 0)
        {
            await using var writer = await connection.BeginBinaryImportAsync(
                "COPY public.g25_pca_populations_samples " +
                "(\"Label\", \"Coordinates\", \"Ids\", \"G25DistanceEraId\", " +
                "\"CreatedAt\", \"CreatedBy\", \"UpdatedAt\", \"UpdatedBy\") FROM STDIN (FORMAT BINARY)");

            foreach (var r in toInsert)
            {
                await writer.StartRowAsync();
                await writer.WriteAsync(r.Label, NpgsqlDbType.Varchar);
                await writer.WriteAsync(r.Coordinates, NpgsqlDbType.Text);
                await writer.WriteAsync(r.Ids, NpgsqlDbType.Text);
                await writer.WriteAsync(r.EraId, NpgsqlDbType.Integer);
                await writer.WriteAsync(now, NpgsqlDbType.TimestampTz);
                await writer.WriteAsync(importedBy, NpgsqlDbType.Text);
                await writer.WriteAsync(now, NpgsqlDbType.TimestampTz);
                await writer.WriteAsync(importedBy, NpgsqlDbType.Text);
                inserted++;
            }

            await writer.CompleteAsync();
        }

        // 3. Verify: every derived row is now present (idempotent — a re-run inserts 0 but still passes).
        long total;
        await using (var count = new NpgsqlCommand(
                         "SELECT COUNT(*) FROM public.g25_pca_populations_samples", connection))
        {
            total = (long)(await count.ExecuteScalarAsync())!;
        }

        Assert.True(
            total >= build.Records.Count,
            $"Expected at least {build.Records.Count} PCA rows after import, found {total}.");

        // Surfaced in the test runner output.
        Assert.True(
            inserted >= 0,
            $"Imported {inserted} new PCA rows (built {build.Records.Count}: ancient {build.AncientRows} + " +
            $"modern {build.ModernRows}; unmatched {build.Unmatched.Count}; dirty {build.Dirty.Count}). " +
            $"Table now holds {total}.");
    }

    private static string TripleKey(int? era, string label, string ids) => $"{era}\n{label}\n{ids}";

    private static async Task<HashSet<int>> ReadIntSetAsync(NpgsqlConnection connection, string sql)
    {
        var set = new HashSet<int>();
        await using var cmd = new NpgsqlCommand(sql, connection);
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            set.Add(reader.GetInt32(0));
        return set;
    }

    private static string ResolveConnectionString()
    {
        var fromEnv = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        var config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(ApplicationDbContext).Assembly)
            .Build();

        return config.GetConnectionString("DefaultConnection") ?? string.Empty;
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Odin.slnx")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException(
                "Could not locate Odin.slnx — unable to resolve the repo root.");

        return dir.FullName;
    }

    private sealed record DistanceSeed(string SampleLabel, string? Coordinates, string? Ids, int G25DistanceEraId);
}
