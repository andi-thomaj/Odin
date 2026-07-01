using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.Admin.Models;
using Odin.Api.Endpoints.G25Calculations;

namespace Odin.Api.Endpoints.Admin;

public interface IG25SeedImportService
{
    Task<ImportG25DistancePopulationSamplesContract.Response> ImportDistancePopulationSamplesAsync(
        string identityId, CancellationToken cancellationToken = default);

    Task<ImportG25PcaPopulationSamplesContract.Response> ImportPcaPopulationSamplesAsync(
        string identityId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Admin-triggered re-import of the G25 distance population sample seed file. Unlike the
/// startup seeder (which short-circuits when the table is non-empty), this runs row-by-row
/// and skips any seed whose <c>SampleLabel</c> already exists in the database — so it can be
/// re-run after the schema has been seeded, to pick up newly added rows in the JSON without
/// touching existing records.
/// </summary>
public class G25SeedImportService(ApplicationDbContext dbContext, IMemoryCache cache) : IG25SeedImportService
{
    private const string ImporterTag = "G25SeedImport";
    private const int BatchSize = 1000;

    public async Task<ImportG25DistancePopulationSamplesContract.Response> ImportDistancePopulationSamplesAsync(
        string identityId, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var path = Path.Combine(
            AppContext.BaseDirectory, "Data", "SeedData", "g25_distance_population_samples.json");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"G25 distance population sample seed file not found at '{path}'. " +
                "Make sure Data/SeedData/g25_distance_population_samples.json is set to copy to the build output.");

        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var seeds = JsonSerializer.Deserialize<List<DistancePopulationSampleSeed>>(
            await File.ReadAllTextAsync(path, cancellationToken), jsonOpts);
        if (seeds is null)
            throw new InvalidOperationException(
                "g25_distance_population_samples.json deserialised to null — check the file contents.");

        var existingLabels = await dbContext.G25DistancePopulationSamples
            .Select(s => s.Label)
            .ToHashSetAsync(cancellationToken);
        var validEraIds = await dbContext.G25DistanceEras
            .Select(e => e.Id)
            .ToHashSetAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var createdBy = string.IsNullOrWhiteSpace(identityId) ? ImporterTag : identityId;
        var batch = new List<G25DistancePopulationSample>(BatchSize);

        var inserted = 0;
        var skippedExistingLabel = 0;
        var skippedInvalidEra = 0;
        var skippedMalformed = 0;
        // Eras that gained samples — their per-era distance-sample cache must be busted after import.
        var affectedEraIds = new HashSet<int>();

        foreach (var seed in seeds)
        {
            if (string.IsNullOrWhiteSpace(seed.SampleLabel) || string.IsNullOrWhiteSpace(seed.Coordinates))
            {
                skippedMalformed++;
                continue;
            }

            if (existingLabels.Contains(seed.SampleLabel))
            {
                skippedExistingLabel++;
                continue;
            }

            if (!validEraIds.Contains(seed.G25DistanceEraId))
            {
                skippedInvalidEra++;
                continue;
            }

            var sample = new G25DistancePopulationSample
            {
                Label = seed.SampleLabel,
                Coordinates = seed.Coordinates,
                Ids = seed.Ids ?? string.Empty,
                G25DistanceEraId = seed.G25DistanceEraId,
                CreatedAt = now,
                CreatedBy = createdBy,
                UpdatedAt = now,
                UpdatedBy = createdBy,
            };

            if (seed.ResearchLinks is { Count: > 0 })
            {
                foreach (var link in seed.ResearchLinks)
                {
                    if (string.IsNullOrWhiteSpace(link.Label) || string.IsNullOrWhiteSpace(link.Link))
                        continue;
                    sample.ResearchLinks.Add(new ResearchLink
                    {
                        Label = link.Label,
                        Link = link.Link,
                        CreatedAt = now,
                        CreatedBy = createdBy,
                        UpdatedAt = now,
                        UpdatedBy = createdBy,
                    });
                }
            }

            batch.Add(sample);
            // Reserve the label immediately so a duplicate Label inside the seed file
            // doesn't get inserted twice within a single run.
            existingLabels.Add(seed.SampleLabel);
            affectedEraIds.Add(seed.G25DistanceEraId);
            inserted++;

            if (batch.Count < BatchSize)
                continue;

            dbContext.G25DistancePopulationSamples.AddRange(batch);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            batch.Clear();
        }

        if (batch.Count > 0)
        {
            dbContext.G25DistancePopulationSamples.AddRange(batch);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
        }

        foreach (var eraId in affectedEraIds)
            cache.Remove(G25SampleCacheKeys.DistanceSamples(eraId));

        stopwatch.Stop();
        return new ImportG25DistancePopulationSamplesContract.Response
        {
            TotalInFile = seeds.Count,
            Inserted = inserted,
            SkippedExistingLabel = skippedExistingLabel,
            SkippedInvalidEra = skippedInvalidEra,
            SkippedMalformed = skippedMalformed,
            DurationMs = stopwatch.ElapsedMilliseconds,
        };
    }

    /// <summary>
    /// Admin-triggered re-import of the G25 PCA population sample seed file (the per-individual reference
    /// cloud). Mirrors <see cref="ImportDistancePopulationSamplesAsync"/>, but because a PCA
    /// <c>Label</c> is NOT unique (many individuals share a population), an existing row is identified by
    /// the <c>(era, label, ids)</c> triple — the same identity the seed generator dedups on. Busts the
    /// per-era PCA scatter cache (<see cref="G25SampleCacheKeys.PcaScatter"/>) for every era that gained rows.
    /// </summary>
    public async Task<ImportG25PcaPopulationSamplesContract.Response> ImportPcaPopulationSamplesAsync(
        string identityId, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var path = Path.Combine(
            AppContext.BaseDirectory, "Data", "SeedData", "g25_pca_population_samples.json");
        if (!File.Exists(path))
            throw new FileNotFoundException(
                $"G25 PCA population sample seed file not found at '{path}'. " +
                "Make sure Data/SeedData/g25_pca_population_samples.json is set to copy to the build output.");

        var jsonOpts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var seeds = JsonSerializer.Deserialize<List<DistancePopulationSampleSeed>>(
            await File.ReadAllTextAsync(path, cancellationToken), jsonOpts);
        if (seeds is null)
            throw new InvalidOperationException(
                "g25_pca_population_samples.json deserialised to null — check the file contents.");

        // Existing-row identity: (era, label, ids). Pulled column-wise then composited in memory so the
        // key matches the generator's dedup key exactly.
        var existingKeys = (await dbContext.G25PcaPopulationsSamples
                .Select(s => new { s.G25DistanceEraId, s.Label, s.Ids })
                .ToListAsync(cancellationToken))
            .Select(s => PcaKey(s.G25DistanceEraId, s.Label, s.Ids))
            .ToHashSet();
        var validEraIds = await dbContext.G25DistanceEras
            .Select(e => e.Id)
            .ToHashSetAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var createdBy = string.IsNullOrWhiteSpace(identityId) ? ImporterTag : identityId;
        var batch = new List<G25PcaPopulationsSample>(BatchSize);

        var inserted = 0;
        var skippedExisting = 0;
        var skippedInvalidEra = 0;
        var skippedMalformed = 0;
        // Eras that gained samples — their per-era PCA scatter cache must be busted after import.
        var affectedEraIds = new HashSet<int>();

        foreach (var seed in seeds)
        {
            if (string.IsNullOrWhiteSpace(seed.SampleLabel) || string.IsNullOrWhiteSpace(seed.Coordinates))
            {
                skippedMalformed++;
                continue;
            }

            if (!validEraIds.Contains(seed.G25DistanceEraId))
            {
                skippedInvalidEra++;
                continue;
            }

            var key = PcaKey(seed.G25DistanceEraId, seed.SampleLabel, seed.Ids ?? string.Empty);
            if (existingKeys.Contains(key))
            {
                skippedExisting++;
                continue;
            }

            var sample = new G25PcaPopulationsSample
            {
                Label = seed.SampleLabel,
                Coordinates = seed.Coordinates,
                Ids = seed.Ids ?? string.Empty,
                G25DistanceEraId = seed.G25DistanceEraId,
                CreatedAt = now,
                CreatedBy = createdBy,
                UpdatedAt = now,
                UpdatedBy = createdBy,
            };

            if (seed.ResearchLinks is { Count: > 0 })
            {
                foreach (var link in seed.ResearchLinks)
                {
                    if (string.IsNullOrWhiteSpace(link.Label) || string.IsNullOrWhiteSpace(link.Link))
                        continue;
                    sample.ResearchLinks.Add(new ResearchLink
                    {
                        Label = link.Label,
                        Link = link.Link,
                        CreatedAt = now,
                        CreatedBy = createdBy,
                        UpdatedAt = now,
                        UpdatedBy = createdBy,
                    });
                }
            }

            batch.Add(sample);
            // Reserve the key so a duplicate triple inside the seed file isn't inserted twice in one run.
            existingKeys.Add(key);
            affectedEraIds.Add(seed.G25DistanceEraId);
            inserted++;

            if (batch.Count < BatchSize)
                continue;

            dbContext.G25PcaPopulationsSamples.AddRange(batch);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
            batch.Clear();
        }

        if (batch.Count > 0)
        {
            dbContext.G25PcaPopulationsSamples.AddRange(batch);
            await dbContext.SaveChangesAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();
        }

        foreach (var eraId in affectedEraIds)
            cache.Remove(G25SampleCacheKeys.PcaScatter(eraId));

        stopwatch.Stop();
        return new ImportG25PcaPopulationSamplesContract.Response
        {
            TotalInFile = seeds.Count,
            Inserted = inserted,
            SkippedExisting = skippedExisting,
            SkippedInvalidEra = skippedInvalidEra,
            SkippedMalformed = skippedMalformed,
            DurationMs = stopwatch.ElapsedMilliseconds,
        };
    }

    private static string PcaKey(int? eraId, string label, string ids) => $"{eraId}\n{label}\n{ids}";

    private sealed record DistancePopulationSampleSeed(
        string SampleLabel,
        string Coordinates,
        string? Ids,
        int G25DistanceEraId,
        List<ResearchLinkSeed>? ResearchLinks);

    private sealed record ResearchLinkSeed(string Label, string Link);
}
