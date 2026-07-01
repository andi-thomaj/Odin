using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.G25Calculations;
using Odin.Api.Endpoints.G25PcaScatter.Models;

namespace Odin.Api.Endpoints.G25PcaScatter;

public interface IG25PcaScatterService
{
    Task<PcaEraScatterContract.Response?> GetEraScatterAsync(int eraId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Serves the per-era PCA scatter (a fitted 2D projection of that era's seeded reference cloud plus the
/// projection basis) for the Ancient Origins PCA tab. Owner-facing and read-mostly, so the assembled
/// per-era response is cached (invalidated whenever a PCA sample in that era changes; skipped under the
/// Testing host env so integration tests read fresh), mirroring the distance-sample cache.
/// </summary>
public class G25PcaScatterService(
    ApplicationDbContext dbContext,
    IMemoryCache cache,
    IHostEnvironment hostEnvironment,
    ILogger<G25PcaScatterService> logger) : IG25PcaScatterService
{
    // Cap on the number of individual points shipped/plotted per era so the ~10.9k Modern cloud stays
    // fast in the browser. The PCA is still FIT on the full cloud — only the plotted points are sampled.
    private const int PlotPointCap = 2500;
    private const int PerPopulationFloor = 3;
    private const int MinSamplesForFit = 10;
    private const int CoordinatePrecision = 5;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(1);

    private bool UseCache => !hostEnvironment.IsEnvironment("Testing");

    public async Task<PcaEraScatterContract.Response?> GetEraScatterAsync(
        int eraId, CancellationToken cancellationToken = default)
    {
        var cacheKey = G25SampleCacheKeys.PcaScatter(eraId);
        if (UseCache && cache.TryGetValue(cacheKey, out PcaEraScatterContract.Response? cached) && cached is not null)
            return cached;

        var era = await dbContext.G25DistanceEras
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == eraId, cancellationToken);
        if (era is null)
            return null;

        var rows = await dbContext.G25PcaPopulationsSamples
            .AsNoTracking()
            .Where(s => s.G25DistanceEraId == eraId)
            .OrderBy(s => s.Id)
            .Select(s => new { s.Label, s.Coordinates })
            .ToListAsync(cancellationToken);

        var labels = new List<string>(rows.Count);
        var coordinates = new List<double[]>(rows.Count);
        foreach (var row in rows)
        {
            // Each row is a whole population's cloud: its Coordinates hold one 25-value group per member
            // individual (groups joined with ';'). Expand into one plotted vector per member, all tagged
            // with the row's population label. A malformed group is skipped without discarding the rest.
            foreach (var vector in ParseCoordinateGroups(row.Coordinates))
            {
                labels.Add(row.Label);
                coordinates.Add(vector);
            }
        }

        var response = Build(era, labels, coordinates);

        if (UseCache)
            cache.Set(cacheKey, response, CacheDuration);
        return response;
    }

    private PcaEraScatterContract.Response Build(G25DistanceEra era, List<string> labels, List<double[]> coordinates)
    {
        var total = coordinates.Count;
        var basis = G25PcaEngine.Fit(coordinates, MinSamplesForFit);

        if (basis is null)
        {
            return new PcaEraScatterContract.Response
            {
                EraId = era.Id,
                EraName = era.Name,
                Basis = null,
                TotalSamples = total,
                PlottedSamples = 0,
            };
        }

        if (basis.NearDegenerate)
            logger.LogWarning(
                "G25 PCA for era {EraId} ('{EraName}') has near-degenerate top eigenvalues " +
                "(λ1={L1:G4}, λ2={L2:G4}); the PC2 axis orientation may be unstable.",
                era.Id, era.Name, basis.EigenX, basis.EigenY);

        // Project the FULL cloud, then downsample only the plotted points.
        var projected = new (string Label, double X, double Y)[total];
        for (var i = 0; i < total; i++)
        {
            var (x, y) = G25PcaEngine.Project(coordinates[i], basis);
            projected[i] = (labels[i], Round(x), Round(y));
        }

        // Per-population centroids from the FULL 25-dim cloud: the 25-dim mean (for the FE to rank
        // clusters by genetic distance to the user) plus its 2D projection (the plotted point). Projecting
        // the mean equals the mean of the projections, so the plotted centroid stays consistent.
        var byLabel = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var i = 0; i < total; i++)
        {
            if (!byLabel.TryGetValue(labels[i], out var indices))
                byLabel[labels[i]] = indices = [];
            indices.Add(i);
        }

        var centroids = byLabel
            .Select(kv =>
            {
                var mean = new double[G25PcaEngine.Dims];
                foreach (var index in kv.Value)
                {
                    var c = coordinates[index];
                    for (var j = 0; j < G25PcaEngine.Dims; j++)
                        mean[j] += c[j];
                }

                for (var j = 0; j < G25PcaEngine.Dims; j++)
                    mean[j] /= kv.Value.Count;

                var (cx, cy) = G25PcaEngine.Project(mean, basis);
                return new PcaEraScatterContract.CentroidDto
                {
                    Label = kv.Key,
                    X = Round(cx),
                    Y = Round(cy),
                    SampleCount = kv.Value.Count,
                    Coordinates = Array.ConvertAll(mean, v => Math.Round(v, 6)),
                };
            })
            .OrderBy(c => c.Label, StringComparer.Ordinal)
            .ToList();

        var points = Downsample(projected);

        return new PcaEraScatterContract.Response
        {
            EraId = era.Id,
            EraName = era.Name,
            Basis = new PcaEraScatterContract.BasisDto
            {
                Means = basis.Means,
                AxisX = basis.AxisX,
                AxisY = basis.AxisY,
            },
            Points = points,
            Centroids = centroids,
            TotalSamples = total,
            PlottedSamples = points.Count,
        };
    }

    // Deterministic, stratified-by-population downsampling: keep every population (with a small floor),
    // allocate the plot budget proportionally to population size, and take an evenly-strided subset of
    // each population's points so the sample is stable across cold fits (no RNG → the cache stays useful).
    private static List<PcaEraScatterContract.PointDto> Downsample((string Label, double X, double Y)[] projected)
    {
        if (projected.Length <= PlotPointCap)
            return projected
                .Select(p => new PcaEraScatterContract.PointDto { Label = p.Label, X = p.X, Y = p.Y })
                .ToList();

        var total = projected.Length;
        var result = new List<PcaEraScatterContract.PointDto>(PlotPointCap + 256);
        foreach (var group in projected.GroupBy(p => p.Label))
        {
            var arr = group.ToArray();
            var size = arr.Length;
            var keep = Math.Min(size, Math.Max(PerPopulationFloor, (int)Math.Ceiling((double)PlotPointCap * size / total)));
            for (var k = 0; k < keep; k++)
            {
                var index = (int)((long)k * size / keep);
                result.Add(new PcaEraScatterContract.PointDto
                {
                    Label = arr[index].Label,
                    X = arr[index].X,
                    Y = arr[index].Y,
                });
            }
        }

        return result;
    }

    // A population row's Coordinates is one 25-value group per member individual, groups joined with ';'
    // (a single-group string — the plain 25-value CSV — parses to one vector, so the old shape still
    // works). Each valid group becomes one vector; a group that isn't exactly 25 numeric values is
    // skipped so one corrupt member costs one point rather than the whole population.
    private static List<double[]> ParseCoordinateGroups(string? raw)
    {
        var vectors = new List<double[]>();
        if (string.IsNullOrWhiteSpace(raw))
            return vectors;

        foreach (var group in raw.Split(
                     ';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = group.Split(',');
            if (parts.Length != G25PcaEngine.Dims)
                continue;

            var parsed = new double[G25PcaEngine.Dims];
            var ok = true;
            for (var i = 0; i < parts.Length; i++)
                if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out parsed[i]))
                {
                    ok = false;
                    break;
                }

            if (ok)
                vectors.Add(parsed);
        }

        return vectors;
    }

    private static double Round(double value) => Math.Round(value, CoordinatePrecision);
}
