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
    /// Build the PCA records — ONE record per POPULATION per era (a cluster), mirroring the distance
    /// dataset, with EVERY member individual's coordinates carried in a single <c>Coordinates</c> string
    /// (25-value groups joined with ';', one group per individual) and the aligned member ids joined with
    /// ','. ANCIENT eras (every era in <paramref name="eraRows"/> except <see cref="ModernEraId"/>): for
    /// each distance sample of that era, resolve each token in its <c>Ids</c> against the era's file
    /// (exact individual-portion match, else boundary-prefix <c>id + "."</c> / <c>id + "_"</c>) and gather
    /// the matched file rows under the sample's friendly label. MODERN era: group every Modern file row
    /// under its population prefix. Within a population the members are de-duplicated on the individual
    /// portion (keeping the first coordinates — this preserves the old (era, label, ids)-triple dedup and
    /// the .AG/.SG double-sequencing) and sorted by individual portion so regenerations are stable.
    /// Coordinate-noise / leaked-label tokens are dropped as dirty; ids absent from the file are reported
    /// unmatched. A population with no matched members produces no record.
    /// </summary>
    public static G25PcaBuildResult Build(
        IReadOnlyList<G25PcaDistanceSample> distanceSamples,
        IReadOnlyDictionary<int, List<G25CoordinateFileRow>> eraRows)
    {
        var unmatched = new List<G25PcaUnmatchedId>();
        var dirty = new List<G25PcaUnmatchedId>();
        var dedupSkipped = 0;

        // (era, label) -> members, keyed by individual portion so a repeated individual (same id listed
        // twice, or two distance samples sharing a label in one era) collapses into a single point.
        var groups = new Dictionary<(int Era, string Label), Dictionary<string, string>>();

        // Add a member to its population group. Returns false (and counts a dedup) if the individual is
        // already present in that group.
        bool AddMember(int era, string label, string individual, string coords)
        {
            var key = (era, label);
            if (!groups.TryGetValue(key, out var members))
                groups[key] = members = new Dictionary<string, string>(StringComparer.Ordinal);

            if (members.ContainsKey(individual))
            {
                dedupSkipped++;
                return false;
            }

            members[individual] = coords;
            return true;
        }

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

        // Ancient eras: gather each distance sample's member individuals into its population group.
        var ancientMembers = 0;
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
                    if (AddMember(era, sample.Label, hit.IndividualPortion, hit.CoordinatesCsv))
                        ancientMembers++;
            }
        }

        // Modern era: group every file row under its population prefix.
        var modernMembers = 0;
        if (eraRows.TryGetValue(ModernEraId, out var modern))
            foreach (var row in modern)
                if (AddMember(ModernEraId, row.PopulationPrefix, row.IndividualPortion, row.CoordinatesCsv))
                    modernMembers++;

        // Emit one record per non-empty population, members sorted by individual portion; records sorted
        // by (era, label) so the generated JSON is stable across regenerations.
        var records = new List<G25PcaSampleRecord>(groups.Count);
        var ancientClusters = 0;
        var modernClusters = 0;
        foreach (var ((era, label), members) in groups)
        {
            if (members.Count == 0) continue;
            var ordered = members
                .OrderBy(m => m.Key, StringComparer.Ordinal)
                .ToList();
            var coords = string.Join(';', ordered.Select(m => m.Value));
            var ids = string.Join(',', ordered.Select(m => m.Key));
            records.Add(new G25PcaSampleRecord(label, coords, ids, era));

            if (era == ModernEraId) modernClusters++;
            else ancientClusters++;
        }

        records.Sort((a, b) =>
        {
            var byEra = a.EraId.CompareTo(b.EraId);
            return byEra != 0 ? byEra : string.CompareOrdinal(a.Label, b.Label);
        });

        return new G25PcaBuildResult(
            records, ancientClusters, modernClusters, ancientMembers, modernMembers, dedupSkipped,
            unmatched, dirty);
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

/// <summary>
/// A derived per-POPULATION PCA sample (a cluster) ready to persist: <c>Coordinates</c> is the members'
/// 25-value groups joined with ';', <c>Ids</c> the aligned member individual portions joined with ','.
/// </summary>
public sealed record G25PcaSampleRecord(string Label, string Coordinates, string Ids, int EraId);

/// <summary>An id that could not be turned into a PCA row (absent from the file, or a dirty token).</summary>
public sealed record G25PcaUnmatchedId(int EraId, string SampleLabel, string Token);

/// <summary>
/// The result of <see cref="G25PcaSeedBuilder.Build"/>: the per-population records (clusters) plus
/// per-category counts for reporting. A "cluster" is one emitted record; "members" are the individual
/// coordinate points aggregated into those records' <c>Coordinates</c> strings.
/// </summary>
public sealed record G25PcaBuildResult(
    IReadOnlyList<G25PcaSampleRecord> Records,
    int AncientClusters,
    int ModernClusters,
    int AncientMembers,
    int ModernMembers,
    int DedupSkipped,
    IReadOnlyList<G25PcaUnmatchedId> Unmatched,
    IReadOnlyList<G25PcaUnmatchedId> Dirty);
