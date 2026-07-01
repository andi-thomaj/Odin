using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Odin.Api.Data;
using Odin.Api.Endpoints.G25Calculations.Models;

namespace Odin.Api.Endpoints.G25Calculations;

public interface IG25CalculationService
{
    Task<(ComputeDistancesContract.Response? Response, string? Error, bool NotFound)> ComputeDistancesAsync(
        ComputeDistancesContract.Request request, CancellationToken ct = default);

    Task<(ComputeAdmixtureSingleContract.Response? Response, string? Error, bool NotFound)> ComputeAdmixtureSingleAsync(
        ComputeAdmixtureSingleContract.Request request, CancellationToken ct = default);

    Task<(ComputeAdmixtureMultiContract.Response? Response, string? Error)> ComputeAdmixtureMultiAsync(
        ComputeAdmixtureMultiContract.Request request, CancellationToken ct = default);
}

/// <summary>
/// Cache keys for G25 reference data. Distance population samples are read-mostly (edited only via
/// the admin sample-management / import endpoints) yet loaded on every distance computation and
/// once per era on every order recompute, so they are cached per era and invalidated on write.
/// </summary>
public static class G25SampleCacheKeys
{
    public static string DistanceSamples(int eraId) => $"g25-distance-samples:{eraId}";

    // Per-era fitted-PCA scatter (basis + projected reference points), served to order owners on the
    // Ancient Origins PCA tab. Invalidated whenever a PCA population sample in that era changes.
    public static string PcaScatter(int eraId) => $"g25-pca-scatter:{eraId}";
}

public class G25CalculationService(
    ApplicationDbContext dbContext,
    IMemoryCache cache,
    IHostEnvironment hostEnvironment,
    ILogger<G25CalculationService> logger) : IG25CalculationService
{
    private const int MaxTargetCsvLength = 64 * 1024;
    private const int MaxTargetRows = 100;
    private const int MaxInlineSourceCsvLength = 4 * 1024 * 1024;

    // TTL safety net in addition to write invalidation, matching the AllEras reference-data cache.
    private static readonly TimeSpan SampleCacheDuration = TimeSpan.FromHours(1);

    public async Task<(ComputeDistancesContract.Response? Response, string? Error, bool NotFound)> ComputeDistancesAsync(
        ComputeDistancesContract.Request request, CancellationToken ct = default)
    {
        var sourceFields = CountSet(request.SourceContent, request.G25DistanceEraId);
        if (sourceFields != 1)
        {
            return (null, "Exactly one of sourceContent or g25DistanceEraId must be provided.", false);
        }

        var (sizeError, _) = ValidateTargetSize(request.TargetCoordinates);
        if (sizeError is not null) return (null, sizeError, false);

        string sourceText;
        if (request.G25DistanceEraId is { } eraId)
        {
            var sampleLines = await GetDistanceSampleLinesAsync(eraId, ct);
            if (sampleLines.Count == 0)
            {
                return (null, $"No distance population samples found for era id {eraId}.", true);
            }
            sourceText = string.Join('\n', sampleLines);
        }
        else
        {
            sourceText = request.SourceContent!;
            if (sourceText.Length > MaxInlineSourceCsvLength)
            {
                return (null, "Source content exceeds the maximum allowed size.", false);
            }
        }

        var parsed = ParseBoth(sourceText, request.TargetCoordinates);
        if (parsed.Error is not null) return (null, parsed.Error, false);

        var maxResults = request.MaxResults is > 0 ? request.MaxResults.Value : 25;

        ct.ThrowIfCancellationRequested();

        // Each target is solved independently against the shared (read-only) source, so fan the
        // CPU-bound work out across the thread pool and preserve input order via the indexed array.
        var targetCount = parsed.Target!.Count;
        var tasks = new Task<ComputeDistancesContract.DistanceTargetResult>[targetCount];
        for (var t = 0; t < targetCount; t++)
        {
            var index = t;
            tasks[index] = Task.Run(
                () => G25AdmixtureSolver.ComputeDistances(parsed.Source!, parsed.Target, index, maxResults),
                ct);
        }

        var results = (await Task.WhenAll(tasks)).ToList();

        return (new ComputeDistancesContract.Response { Results = results }, null, false);
    }

    public async Task<(ComputeAdmixtureSingleContract.Response? Response, string? Error, bool NotFound)> ComputeAdmixtureSingleAsync(
        ComputeAdmixtureSingleContract.Request request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(request.SourceContent))
        {
            return (null, "sourceContent is required for single admixture.", false);
        }

        var (sizeError, _) = ValidateTargetSize(request.TargetCoordinates);
        if (sizeError is not null) return (null, sizeError, false);

        if (request.SourceContent.Length > MaxInlineSourceCsvLength)
        {
            return (null, "Source content exceeds the maximum allowed size.", false);
        }

        var parsed = ParseBoth(request.SourceContent, request.TargetCoordinates);
        if (parsed.Error is not null) return (null, parsed.Error, false);

        var cyclesMultiplier = request.CyclesMultiplier is > 0 ? request.CyclesMultiplier.Value : 1.0;
        var slots = request.Slots is > 0 ? request.Slots.Value : 500;
        var aggregate = request.Aggregate ?? true;
        var printZeroes = request.PrintZeroes ?? false;

        ct.ThrowIfCancellationRequested();

        // Targets are independent; run the per-target solves concurrently rather than awaiting each
        // in turn. The indexed array keeps the response in the same order as the request.
        var targetCount = parsed.Target!.Count;
        var tasks = new Task<ComputeAdmixtureSingleContract.AdmixtureSingleResult>[targetCount];
        for (var t = 0; t < targetCount; t++)
        {
            var index = t;
            tasks[index] = Task.Run(
                () => G25AdmixtureSolver.ComputeSingle(
                    parsed.Source!, parsed.Target, index, cyclesMultiplier, slots, aggregate, printZeroes, ct),
                ct);
        }

        var results = (await Task.WhenAll(tasks)).ToList();

        return (new ComputeAdmixtureSingleContract.Response { Results = results }, null, false);
    }

    public async Task<(ComputeAdmixtureMultiContract.Response? Response, string? Error)> ComputeAdmixtureMultiAsync(
        ComputeAdmixtureMultiContract.Request request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(request.SourceContent))
        {
            return (null, "sourceContent is required for multi admixture.");
        }

        var (sizeError, _) = ValidateTargetSize(request.TargetCoordinates);
        if (sizeError is not null) return (null, sizeError);

        if (request.SourceContent.Length > MaxInlineSourceCsvLength)
        {
            return (null, "Source content exceeds the maximum allowed size.");
        }

        var parsed = ParseBoth(request.SourceContent, request.TargetCoordinates);
        if (parsed.Error is not null) return (null, parsed.Error);

        var cyclesMultiplier = request.CyclesMultiplier is > 0 ? request.CyclesMultiplier.Value : 1.0;
        var fastMode = request.FastMode ?? false;
        var aggregate = request.Aggregate ?? true;
        var printZeroes = request.PrintZeroes ?? false;

        var response = await Task.Run(
            () => G25AdmixtureSolver.ComputeMulti(
                parsed.Source!, parsed.Target!, cyclesMultiplier, fastMode, aggregate, printZeroes, ct),
            ct);

        return (response, null);
    }

    /// <summary>
    /// Returns the pre-formatted source lines for an era's distance population samples, caching the
    /// result per era. The samples are stable reference data, so this turns the per-computation (and
    /// per-era-per-inspection recompute) DB query + line formatting into a single warm-cache lookup.
    /// Invalidated by the sample-management / import endpoints via <see cref="G25SampleCacheKeys"/>.
    /// </summary>
    private async Task<IReadOnlyList<string>> GetDistanceSampleLinesAsync(int eraId, CancellationToken ct)
    {
        var cacheKey = G25SampleCacheKeys.DistanceSamples(eraId);

        if (!hostEnvironment.IsEnvironment("Testing") &&
            cache.TryGetValue(cacheKey, out IReadOnlyList<string>? cached))
            return cached!;

        var samples = await dbContext.G25DistancePopulationSamples
            .AsNoTracking()
            .Where(s => s.G25DistanceEraId == eraId)
            .Select(s => new { s.Label, s.Coordinates })
            .ToListAsync(ct);

        var (lines, skippedLabels) = SelectValidSampleLines(samples.Select(s => (s.Label, s.Coordinates)));
        if (skippedLabels.Count > 0)
        {
            // One malformed reference sample (e.g. a citation/URL pasted into Coordinates) must not fail
            // the whole era's parse and wipe distance results for every user — drop it and log it instead.
            logger.LogWarning(
                "G25 distance era {EraId}: skipped {Count} malformed reference sample(s) (non-numeric or " +
                "inconsistent-length coordinates). Examples: {Labels}.",
                eraId, skippedLabels.Count, string.Join(" | ", skippedLabels.Take(10)));
        }

        if (!hostEnvironment.IsEnvironment("Testing"))
        {
            cache.Set(cacheKey, lines, new MemoryCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = SampleCacheDuration
            });
        }

        return lines;
    }

    private static string BuildSampleLine(string label, string coordinates)
    {
        var trimmed = coordinates.Trim();
        var firstBreak = trimmed.IndexOfAny(['\r', '\n']);
        if (firstBreak >= 0)
        {
            trimmed = trimmed[..firstBreak].TrimEnd();
        }
        var firstComma = trimmed.IndexOf(',');
        if (firstComma < 0) return $"{label},{trimmed}";
        if (firstComma == 0) return $"{label}{trimmed}";

        var leader = trimmed[..firstComma].Trim();
        if (double.TryParse(leader, NumberStyles.Float, CultureInfo.InvariantCulture, out _))
        {
            return $"{label},{trimmed}";
        }
        return $"{label},{trimmed[(firstComma + 1)..]}";
    }

    /// <summary>
    /// Builds the "Label,v1,...,vN" source line for each sample and keeps only the well-formed ones:
    /// every value numeric and a consistent column count across the era (the majority width). A single
    /// malformed reference sample — e.g. a DOI/URL or a paper title accidentally pasted into the
    /// Coordinates field — would otherwise make <see cref="G25CoordinateParser"/> reject the ENTIRE era
    /// (variable-column / non-numeric error), wiping distance results for every user. Returns the kept
    /// lines plus the labels of the samples that were dropped (for logging).
    /// </summary>
    public static (List<string> Lines, List<string> SkippedLabels) SelectValidSampleLines(
        IEnumerable<(string Label, string Coordinates)> samples)
    {
        var candidates = new List<(string Label, string Line, int ValueCount)>();
        var skipped = new List<string>();

        foreach (var (label, coordinates) in samples)
        {
            var line = BuildSampleLine(label, coordinates);
            var columns = line.Split(',');
            if (columns.Length < 2 || !AllNumeric(columns, 1))
            {
                skipped.Add(label);
                continue;
            }

            candidates.Add((label, line, columns.Length - 1));
        }

        if (candidates.Count == 0)
            return (new List<string>(), skipped);

        // Keep the majority column width; drop numeric-but-wrong-length outliers (a sample with too few
        // or too many values would still trip the parser's "variable column number" check).
        var modalValueCount = candidates
            .GroupBy(c => c.ValueCount)
            .OrderByDescending(g => g.Count())
            .ThenByDescending(g => g.Key)
            .First().Key;

        var lines = new List<string>(candidates.Count);
        foreach (var candidate in candidates)
        {
            if (candidate.ValueCount == modalValueCount)
                lines.Add(candidate.Line);
            else
                skipped.Add(candidate.Label);
        }

        return (lines, skipped);
    }

    private static bool AllNumeric(string[] columns, int startIndex)
    {
        for (var j = startIndex; j < columns.Length; j++)
        {
            if (!double.TryParse(columns[j].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                return false;
        }

        return true;
    }

    private static int CountSet(params object?[] fields)
    {
        var n = 0;
        foreach (var f in fields)
        {
            if (f is string s) { if (!string.IsNullOrEmpty(s)) n++; }
            else if (f is not null) n++;
        }
        return n;
    }

    private static (string? Error, int Rows) ValidateTargetSize(string? targetCoordinates)
    {
        if (string.IsNullOrWhiteSpace(targetCoordinates))
        {
            return ("Target coordinates are required.", 0);
        }
        if (targetCoordinates.Length > MaxTargetCsvLength)
        {
            return ("Target coordinates exceed the maximum allowed size.", 0);
        }
        var rowEstimate = 1 + targetCoordinates.Count(c => c == '\n');
        if (rowEstimate > MaxTargetRows)
        {
            return ($"Target coordinates exceed the maximum allowed row count ({MaxTargetRows}).", rowEstimate);
        }
        return (null, rowEstimate);
    }

    private readonly struct ParsedInput
    {
        public readonly IReadOnlyList<G25CoordinateParser.CoordinateRow>? Source;
        public readonly IReadOnlyList<G25CoordinateParser.CoordinateRow>? Target;
        public readonly string? Error;

        public ParsedInput(
            IReadOnlyList<G25CoordinateParser.CoordinateRow>? source,
            IReadOnlyList<G25CoordinateParser.CoordinateRow>? target,
            string? error)
        {
            Source = source;
            Target = target;
            Error = error;
        }
    }

    private static ParsedInput ParseBoth(string sourceText, string targetText)
    {
        var src = G25CoordinateParser.Parse(sourceText, "SOURCE");
        if (src.Errors != 0 || src.Lines is null)
        {
            return new ParsedInput(null, null, src.Message.Trim());
        }

        var tgt = G25CoordinateParser.Parse(targetText, "TARGET");
        if (tgt.Errors != 0 || tgt.Lines is null)
        {
            return new ParsedInput(null, null, tgt.Message.Trim());
        }

        if (src.Lines.Count == 0 || tgt.Lines.Count == 0)
        {
            return new ParsedInput(null, null, "ERROR! Empty input.");
        }

        if (src.Lines[0].Length != tgt.Lines[0].Length)
        {
            return new ParsedInput(null, null, "ERROR! Column number mismatch.");
        }

        return new ParsedInput(src.Lines, tgt.Lines, null);
    }
}
