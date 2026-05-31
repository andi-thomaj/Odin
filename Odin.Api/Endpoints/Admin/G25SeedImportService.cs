using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.Admin.Models;

namespace Odin.Api.Endpoints.Admin;

public interface IG25SeedImportService
{
    Task<ImportG25DistancePopulationSamplesContract.Response> ImportDistancePopulationSamplesAsync(
        string identityId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Admin-triggered re-import of the G25 distance population sample seed file. Unlike the
/// startup seeder (which short-circuits when the table is non-empty), this runs row-by-row
/// and skips any seed whose <c>SampleLabel</c> already exists in the database — so it can be
/// re-run after the schema has been seeded, to pick up newly added rows in the JSON without
/// touching existing records.
/// </summary>
public class G25SeedImportService(ApplicationDbContext dbContext) : IG25SeedImportService
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

    private sealed record DistancePopulationSampleSeed(
        string SampleLabel,
        string Coordinates,
        string? Ids,
        int G25DistanceEraId,
        List<ResearchLinkSeed>? ResearchLinks);

    private sealed record ResearchLinkSeed(string Label, string Link);
}
