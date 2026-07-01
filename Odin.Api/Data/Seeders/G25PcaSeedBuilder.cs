namespace Odin.Api.Data.Seeders;

/// <summary>
/// Pure (no I/O, no EF) derivation of the G25 PCA population samples — the per-individual reference
/// cloud — from the distance samples (population averages, which carry the member individual ids) and
/// the per-era G25 coordinate files. Shared by the offline generator (<c>tools/GenerateG25PcaSeed</c>)
/// and the on-demand import test (<c>G25PcaPopulationSamplesImportTests</c>) so the matching rules live
/// in exactly one place. See <see cref="Build"/> for the ancient-vs-modern rules.
/// </summary>
public static class G25PcaSeedBuilder
{
    /// <summary>Modern era id (G25DistanceEra catalog, seeded 1..6). Modern samples have no ids.</summary>
    public const int ModernEraId = 6;

    /// <summary>Number of principal components per sample (PC1..PC25).</summary>
    public const int CoordinateCount = 25;

    /// <summary>
    /// EraId -> coordinate file name. Ids in a distance sample are looked up in the SAME era's file.
    /// Filenames use a hyphen where the DB era names use an en-dash, but we key on the numeric era id,
    /// so no dash normalisation is needed.
    /// </summary>
    public static readonly IReadOnlyDictionary<int, string> EraFileNames = new Dictionary<int, string>
    {
        [1] = "Late Bronze Age (3000-1200 BC).txt",
        [2] = "Pre-Classical Iron Age (1200-0 BC).txt",
        [3] = "Imperial Antiquity (0-600 AD).txt",
        [4] = "Middle Ages (600-1400 AD).txt",
        [5] = "Early Modern Period (1400-2000 AD).txt",
        [6] = "Modern Era (2000-2026 AD).txt",
    };

    /// <summary>
    /// Parse the lines of one coordinate file into rows. Each data line is
    /// <c>Population:IndividualID,PC1,...,PC25</c>. Header/blank lines (empty label before the first
    /// comma — this covers the Modern file's ",PC1,..." header) and any row that doesn't carry exactly
    /// <see cref="CoordinateCount"/> PC values are skipped.
    /// </summary>
    public static List<G25CoordinateFileRow> ParseFileRows(IEnumerable<string> lines)
    {
        var rows = new List<G25CoordinateFileRow>();
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var firstComma = line.IndexOf(',');
            if (firstComma <= 0) continue; // header (",PC1,...") or a label-less line

            var label = line[..firstComma];
            var coords = line[(firstComma + 1)..].Trim();
            if (CountChar(coords, ',') != CoordinateCount - 1) continue; // not 25 values

            var lastColon = label.LastIndexOf(':');
            var individual = lastColon >= 0 ? label[(lastColon + 1)..] : label;
            var population = lastColon > 0 ? label[..lastColon] : label;
            rows.Add(new G25CoordinateFileRow(individual, population, coords));
        }

        return rows;
    }

    /// <summary>
    /// Build the PCA records. ANCIENT eras (every era in <paramref name="eraRows"/> except
    /// <see cref="ModernEraId"/>): for each distance sample of that era, resolve each token in its
    /// <c>Ids</c> against the era's file (exact individual-portion match, else boundary-prefix
    /// <c>id + "."</c> / <c>id + "_"</c>) and emit one PCA row per matched file row, with the sample's
    /// friendly label. MODERN era: ingest every Modern file row as-is (label = the file's population
    /// prefix). Coordinate-noise / leaked-label tokens are dropped as dirty; ids absent from the file
    /// are reported unmatched. Output is de-duplicated on the (era, label, ids) triple.
    /// </summary>
    public static G25PcaBuildResult Build(
        IReadOnlyList<G25PcaDistanceSample> distanceSamples,
        IReadOnlyDictionary<int, List<G25CoordinateFileRow>> eraRows)
    {
        var records = new List<G25PcaSampleRecord>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var unmatched = new List<G25PcaUnmatchedId>();
        var dirty = new List<G25PcaUnmatchedId>();
        var dedupSkipped = 0;
        var ancientRows = 0;

        // Exact-match index per ancient era (individual portion -> rows; a token can map to >1 row,
        // e.g. an individual sequenced twice as .AG and .SG).
        var exactIndex = new Dictionary<int, Dictionary<string, List<G25CoordinateFileRow>>>();
        foreach (var (eraId, rows) in eraRows)
        {
            if (eraId == ModernEraId) continue;
            var map = new Dictionary<string, List<G25CoordinateFileRow>>(StringComparer.Ordinal);
            foreach (var row in rows)
            {
                if (!map.TryGetValue(row.IndividualPortion, out var list))
                    map[row.IndividualPortion] = list = new List<G25CoordinateFileRow>();
                list.Add(row);
            }

            exactIndex[eraId] = map;
        }

        bool TryAdd(string label, string coords, string ids, int era)
        {
            if (!seen.Add($"{era}\n{label}\n{ids}"))
            {
                dedupSkipped++;
                return false;
            }

            records.Add(new G25PcaSampleRecord(label, coords, ids, era));
            return true;
        }

        // Ancient eras: derive individuals from each distance sample's Ids.
        foreach (var sample in distanceSamples)
        {
            var era = sample.EraId;
            if (era == ModernEraId) continue;
            if (!exactIndex.TryGetValue(era, out var exact)) continue;
            if (string.IsNullOrWhiteSpace(sample.Ids)) continue;

            var tokensSeen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var raw in sample.Ids.Split(','))
            {
                var token = raw.Trim();
                if (token.Length == 0) continue;
                if (!tokensSeen.Add(token)) continue;

                if (IsDirtyToken(token))
                {
                    dirty.Add(new G25PcaUnmatchedId(era, sample.Label, token));
                    continue;
                }

                var hits = MatchId(token, exact, eraRows[era]);
                if (hits.Count == 0)
                {
                    unmatched.Add(new G25PcaUnmatchedId(era, sample.Label, token));
                    continue;
                }

                foreach (var hit in hits)
                    if (TryAdd(sample.Label, hit.CoordinatesCsv, hit.IndividualPortion, era))
                        ancientRows++;
            }
        }

        // Modern era: ingest every file row as-is.
        var modernRows = 0;
        if (eraRows.TryGetValue(ModernEraId, out var modern))
            foreach (var row in modern)
                if (TryAdd(row.PopulationPrefix, row.CoordinatesCsv, row.IndividualPortion, ModernEraId))
                    modernRows++;

        return new G25PcaBuildResult(records, ancientRows, modernRows, dedupSkipped, unmatched, dirty);
    }

    // A stored id matches a file row whose individual portion equals it, or begins with it followed by
    // a boundary ('.' or '_') — e.g. "I14689" -> "I14689.AG__BC_2555__Cov_50.21%". Exact matches win;
    // only with no exact match do we consider boundary-prefix matches (which may return several rows).
    private static List<G25CoordinateFileRow> MatchId(
        string id, Dictionary<string, List<G25CoordinateFileRow>> exact, List<G25CoordinateFileRow> all)
    {
        if (exact.TryGetValue(id, out var exactHits))
            return exactHits;

        var hits = new List<G25CoordinateFileRow>();
        foreach (var row in all)
        {
            var ind = row.IndividualPortion;
            if (ind.Length <= id.Length) continue;
            if (!ind.StartsWith(id, StringComparison.Ordinal)) continue;
            var boundary = ind[id.Length];
            if (boundary == '.' || boundary == '_')
                hits.Add(row);
        }

        return hits;
    }

    // Tokens that aren't real sample ids: coordinate numbers or a whole label leaked into the Ids
    // column (contains ':'/'*' or is a decimal number). A bare integer with no dot (e.g. "12726") is a
    // genuine-but-typo'd id, not dirty — it simply won't match and is reported as unmatched.
    private static bool IsDirtyToken(string token)
    {
        if (token.Contains(':') || token.Contains('*'))
            return true;

        if (token.Contains('.'))
        {
            var body = token.TrimStart('-');
            if (body.Length > 0 && body.All(c => char.IsDigit(c) || c == '.'))
                return true;
        }

        return false;
    }

    private static int CountChar(string s, char c)
    {
        var n = 0;
        foreach (var ch in s)
            if (ch == c)
                n++;
        return n;
    }
}

/// <summary>A parsed coordinate-file row: the individual id portion, its population prefix, and the raw 25-value PC csv.</summary>
public sealed record G25CoordinateFileRow(string IndividualPortion, string PopulationPrefix, string CoordinatesCsv);

/// <summary>A distance population sample as far as PCA derivation cares: friendly label, member ids, era.</summary>
public sealed record G25PcaDistanceSample(string Label, string? Ids, int EraId);

/// <summary>A derived per-individual PCA sample ready to persist.</summary>
public sealed record G25PcaSampleRecord(string Label, string Coordinates, string Ids, int EraId);

/// <summary>An id that could not be turned into a PCA row (absent from the file, or a dirty token).</summary>
public sealed record G25PcaUnmatchedId(int EraId, string SampleLabel, string Token);

/// <summary>The result of <see cref="G25PcaSeedBuilder.Build"/>: the records plus per-category counts for reporting.</summary>
public sealed record G25PcaBuildResult(
    IReadOnlyList<G25PcaSampleRecord> Records,
    int AncientRows,
    int ModernRows,
    int DedupSkipped,
    IReadOnlyList<G25PcaUnmatchedId> Unmatched,
    IReadOnlyList<G25PcaUnmatchedId> Dirty);
