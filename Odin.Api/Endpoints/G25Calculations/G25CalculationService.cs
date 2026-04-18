using Microsoft.EntityFrameworkCore;
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

public class G25CalculationService(ApplicationDbContext dbContext) : IG25CalculationService
{
    private const int MaxTargetCsvLength = 64 * 1024;
    private const int MaxTargetRows = 100;
    private const int MaxInlineSourceCsvLength = 4 * 1024 * 1024;
    private const int MaxSelectedRegions = 16;

    public async Task<(ComputeDistancesContract.Response? Response, string? Error, bool NotFound)> ComputeDistancesAsync(
        ComputeDistancesContract.Request request, CancellationToken ct = default)
    {
        var sourceFields = CountSet(request.SourceContent, request.SourceDistanceFileId, request.G25EraId);
        if (sourceFields != 1)
        {
            return (null, "Exactly one of sourceContent, sourceDistanceFileId, or g25EraId must be provided.", false);
        }

        var (sizeError, _) = ValidateTargetSize(request.TargetCoordinates);
        if (sizeError is not null) return (null, sizeError, false);

        string sourceText;
        if (request.SourceDistanceFileId is { } fileId)
        {
            var content = await dbContext.G25DistanceFiles
                .AsNoTracking()
                .Where(f => f.Id == fileId)
                .Select(f => f.Content)
                .FirstOrDefaultAsync(ct);
            if (content is null) return (null, null, true);
            sourceText = content;
        }
        else if (request.G25EraId is { } eraId)
        {
            var content = await dbContext.G25DistanceFiles
                .AsNoTracking()
                .Where(f => f.G25EraId == eraId)
                .Select(f => f.Content)
                .FirstOrDefaultAsync(ct);
            if (content is null) return (null, $"No distance file found for era id {eraId}.", true);
            sourceText = content;
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

        var results = new List<ComputeDistancesContract.DistanceTargetResult>(parsed.Target!.Count);
        for (var t = 0; t < parsed.Target.Count; t++)
        {
            ct.ThrowIfCancellationRequested();
            results.Add(G25AdmixtureSolver.ComputeDistances(parsed.Source!, parsed.Target, t, maxResults));
        }

        return (new ComputeDistancesContract.Response { Results = results }, null, false);
    }

    public async Task<(ComputeAdmixtureSingleContract.Response? Response, string? Error, bool NotFound)> ComputeAdmixtureSingleAsync(
        ComputeAdmixtureSingleContract.Request request, CancellationToken ct = default)
    {
        var hasRegions = request.SourceRegionIds is { Count: > 0 };
        var sourceFields = CountSet(request.SourceContent, request.SourceAdmixtureFileId, hasRegions ? (object)true : null);
        if (sourceFields != 1)
        {
            return (null, "Exactly one of sourceContent, sourceAdmixtureFileId, or sourceRegionIds must be provided.", false);
        }

        var (sizeError, _) = ValidateTargetSize(request.TargetCoordinates);
        if (sizeError is not null) return (null, sizeError, false);

        string sourceText;
        if (request.SourceAdmixtureFileId is { } fileId)
        {
            var content = await dbContext.G25AdmixtureFiles
                .AsNoTracking()
                .Where(f => f.Id == fileId)
                .Select(f => f.Content)
                .FirstOrDefaultAsync(ct);
            if (content is null) return (null, null, true);
            sourceText = content;
        }
        else if (hasRegions)
        {
            var regionIds = request.SourceRegionIds!.Distinct().ToList();
            if (regionIds.Count > MaxSelectedRegions)
            {
                return (null, $"At most {MaxSelectedRegions} regions can be selected.", false);
            }

            var files = await dbContext.G25AdmixtureFiles
                .AsNoTracking()
                .Where(f => regionIds.Contains(f.G25RegionId))
                .ToListAsync(ct);

            if (files.Count != regionIds.Count)
            {
                var found = files.Select(f => f.G25RegionId).ToHashSet();
                var missing = regionIds.Where(id => !found.Contains(id)).ToList();
                return (null, $"No admixture file(s) found for region id(s) [{string.Join(", ", missing)}].", true);
            }

            sourceText = MergeAdmixtureFileContents(files.Select(f => f.Content));
            if (sourceText.Length > MaxInlineSourceCsvLength)
            {
                return (null, "Merged source content exceeds the maximum allowed size.", false);
            }
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

        var cyclesMultiplier = request.CyclesMultiplier is > 0 ? request.CyclesMultiplier.Value : 1.0;
        var slots = request.Slots is > 0 ? request.Slots.Value : 500;
        var aggregate = request.Aggregate ?? true;
        var printZeroes = request.PrintZeroes ?? false;

        var results = new List<ComputeAdmixtureSingleContract.AdmixtureSingleResult>(parsed.Target!.Count);
        for (var t = 0; t < parsed.Target.Count; t++)
        {
            ct.ThrowIfCancellationRequested();
            var res = await Task.Run(
                () => G25AdmixtureSolver.ComputeSingle(
                    parsed.Source!, parsed.Target, t, cyclesMultiplier, slots, aggregate, printZeroes, ct),
                ct);
            results.Add(res);
        }

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

    private static string MergeAdmixtureFileContents(IEnumerable<string> contents)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var merged = new List<string>();
        foreach (var content in contents)
        {
            if (string.IsNullOrWhiteSpace(content)) continue;
            var normalized = content.Replace("\r\n", "\n");
            foreach (var line in normalized.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.Length == 0) continue;
                var commaIdx = trimmed.IndexOf(',');
                var name = commaIdx >= 0 ? trimmed[..commaIdx].Trim() : trimmed;
                if (seen.Add(name)) merged.Add(trimmed);
            }
        }
        return string.Join('\n', merged);
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
