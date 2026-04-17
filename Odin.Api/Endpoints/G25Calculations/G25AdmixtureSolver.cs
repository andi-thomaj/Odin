using System.Globalization;
using Odin.Api.Endpoints.G25Calculations.Models;

namespace Odin.Api.Endpoints.G25Calculations;

/// <summary>
/// Port of the client-side Monte-Carlo admixture solver from
/// <c>odin-react/src/features/admixture/admixture-engine.ts</c>. The algorithm
/// is intrinsically non-deterministic; see unit tests for tolerance-based checks.
/// </summary>
public static class G25AdmixtureSolver
{
    private const double IntegerScale = 1e17;

    public static ComputeDistancesContract.DistanceTargetResult ComputeDistances(
        IReadOnlyList<G25CoordinateParser.CoordinateRow> source,
        IReadOnlyList<G25CoordinateParser.CoordinateRow> target,
        int targetId,
        int maxOut)
    {
        var t = target[targetId];
        var targetName = t.Name;
        var targetNums = t.Values;
        var srcCount = source.Count;

        var dists = new List<ComputeDistancesContract.DistanceRow>(srcCount);
        for (var i = 0; i < srcCount; i++)
        {
            var src = source[i];
            if (src.Name == targetName) continue;

            var diff = Subtract(targetNums, src.Values);
            var d = Math.Sqrt(SquareSum(diff));
            dists.Add(new ComputeDistancesContract.DistanceRow
            {
                Name = src.Name,
                Distance = d
            });
        }
        dists.Sort((a, b) => a.Distance.CompareTo(b.Distance));

        var take = Math.Min(maxOut, dists.Count);
        return new ComputeDistancesContract.DistanceTargetResult
        {
            TargetName = targetName,
            Rows = dists.GetRange(0, take)
        };
    }

    public static ComputeAdmixtureSingleContract.AdmixtureSingleResult ComputeSingle(
        IReadOnlyList<G25CoordinateParser.CoordinateRow> source,
        IReadOnlyList<G25CoordinateParser.CoordinateRow> target,
        int targetId,
        double cyclesMultiplier,
        int slots,
        bool aggregate,
        bool printZeroes,
        CancellationToken ct)
    {
        var srcCount = source.Count;

        var targetRow = target[targetId];
        var targetName = targetRow.Name;
        var targetScaled = ScaleBySlots(targetRow.Values, slots);
        var sourceScaled = ScaleSourceBySlots(source, slots);

        var result = FastMonteCarlo(targetScaled, sourceScaled, slots, cyclesMultiplier, srcCount, ct);

        var table = new List<TableRow>(srcCount);
        for (var i = 0; i < srcCount; i++)
        {
            table.Add(new TableRow
            {
                Name = source[i].Name,
                Scores = new[] { result.Scores[i] }
            });
        }

        if (aggregate)
        {
            table = AggregateResults(table);
        }

        table.Sort((a, b) => b.Scores[0].CompareTo(a.Scores[0]));

        var rows = new List<ComputeAdmixtureSingleContract.AdmixtureRow>(table.Count);
        foreach (var row in table)
        {
            var score = row.Scores[0];
            if (!printZeroes && score == 0) continue;
            rows.Add(new ComputeAdmixtureSingleContract.AdmixtureRow
            {
                Name = row.Name,
                Pct = score * 100.0
            });
        }

        return new ComputeAdmixtureSingleContract.AdmixtureSingleResult
        {
            TargetName = targetName,
            Distance = result.Distance,
            DistancePct = (result.Distance * 100.0).ToString("F4", CultureInfo.InvariantCulture),
            Rows = rows
        };
    }

    public static ComputeAdmixtureMultiContract.Response ComputeMulti(
        IReadOnlyList<G25CoordinateParser.CoordinateRow> source,
        IReadOnlyList<G25CoordinateParser.CoordinateRow> target,
        double cyclesMultiplier,
        bool fastMode,
        bool aggregate,
        bool printZeroes,
        CancellationToken ct)
    {
        var srcCount = source.Count;
        var tgtCount = target.Count;
        var slots = fastMode ? 125 : 500;

        var results = new FmcResult[tgtCount];
        var sourceScaled = ScaleSourceBySlots(source, slots);
        for (var i = 0; i < tgtCount; i++)
        {
            ct.ThrowIfCancellationRequested();
            var targetScaled = ScaleBySlots(target[i].Values, slots);
            results[i] = FastMonteCarlo(targetScaled, sourceScaled, slots, cyclesMultiplier, srcCount, ct);
        }

        var table = new List<TableRow>(srcCount);
        for (var i = 0; i < srcCount; i++)
        {
            var scores = new double[tgtCount];
            for (var t = 0; t < tgtCount; t++) scores[t] = results[t].Scores[i];
            table.Add(new TableRow { Name = source[i].Name, Scores = scores });
        }

        if (aggregate)
        {
            table = AggregateResults(table);
        }

        var accumulated = new double[table.Count];
        for (var t = 0; t < tgtCount; t++)
        {
            for (var i = 0; i < table.Count; i++)
            {
                accumulated[i] += table[i].Scores[t];
            }
        }

        if (!printZeroes)
        {
            var keptIdx = new List<int>(table.Count);
            for (var i = 0; i < table.Count; i++)
            {
                if (accumulated[i] != 0) keptIdx.Add(i);
            }
            table = keptIdx.Select(i => table[i]).ToList();
            accumulated = keptIdx.Select(i => accumulated[i]).ToArray();
        }

        var sourceNames = table.Select(r => r.Name).ToList();

        var targetsOut = new ComputeAdmixtureMultiContract.AdmixtureMultiTarget[tgtCount];
        double avgDist = 0;
        for (var t = 0; t < tgtCount; t++)
        {
            avgDist += results[t].Distance;
            var scoresPct = new double[table.Count];
            for (var i = 0; i < table.Count; i++)
            {
                scoresPct[i] = table[i].Scores[t] * 100.0;
            }
            targetsOut[t] = new ComputeAdmixtureMultiContract.AdmixtureMultiTarget
            {
                Name = target[t].Name,
                Distance = results[t].Distance,
                Scores = scoresPct
            };
        }
        if (tgtCount > 0) avgDist /= tgtCount;

        var averageScores = new double[table.Count];
        for (var i = 0; i < table.Count; i++)
        {
            double sum = 0;
            for (var t = 0; t < tgtCount; t++) sum += table[i].Scores[t];
            averageScores[i] = tgtCount > 0 ? (sum / tgtCount) * 100.0 : 0;
        }

        return new ComputeAdmixtureMultiContract.Response
        {
            SourceNames = sourceNames,
            Targets = targetsOut,
            AverageDistance = avgDist,
            AverageScores = averageScores
        };
    }

    private sealed class TableRow
    {
        public required string Name { get; set; }
        public required double[] Scores { get; set; }
    }

    private static List<TableRow> AggregateResults(List<TableRow> table)
    {
        var prefixed = table
            .Select(r => new TableRow
            {
                Name = r.Name.Split(':')[0],
                Scores = (double[])r.Scores.Clone()
            })
            .OrderBy(r => r.Name, StringComparer.Ordinal)
            .ToList();

        for (var i = prefixed.Count - 2; i >= 0; i--)
        {
            if (prefixed[i].Name == prefixed[i + 1].Name)
            {
                for (var j = 0; j < prefixed[i].Scores.Length; j++)
                {
                    prefixed[i].Scores[j] += prefixed[i + 1].Scores[j];
                }
                prefixed.RemoveAt(i + 1);
            }
        }
        return prefixed;
    }

    private readonly struct FmcResult
    {
        public readonly double Distance;
        public readonly double[] Scores;

        public FmcResult(double distance, double[] scores)
        {
            Distance = distance;
            Scores = scores;
        }
    }

    private static FmcResult FastMonteCarlo(
        double[] target,
        double[][] source,
        int slots,
        double cyclesMultiplier,
        int srcCount,
        CancellationToken ct)
    {
        var dimNum = target.Length;
        var scores = new double[srcCount];

        var centered = new double[srcCount][];
        for (var s = 0; s < srcCount; s++)
        {
            centered[s] = Subtract(source[s], target);
        }

        if (srcCount == 1)
        {
            var pt = new double[dimNum];
            for (var d = 0; d < dimNum; d++)
            {
                for (var s = 0; s < slots; s++) pt[d] += centered[0][d];
            }
            scores[0] = 1.0;
            return new FmcResult(Math.Round(Math.Sqrt(SquareSum(pt)), 8), scores);
        }

        var cycles = (int)Math.Ceiling((srcCount * cyclesMultiplier) / 4.0);

        var rnd = Random.Shared;
        var curSlots = new int[slots];
        for (var s = 0; s < slots; s++) curSlots[s] = rnd.Next(srcCount);

        var curPoint = new double[dimNum];
        for (var s = 0; s < slots; s++)
        {
            var c = centered[curSlots[s]];
            for (var d = 0; d < dimNum; d++) curPoint[d] += c[d];
        }
        var curDist = SquareSum(curPoint);

        var nextPoint = new double[dimNum];

        for (var c = 0; c < cycles; c++)
        {
            ct.ThrowIfCancellationRequested();
            for (var j = 0; j < slots; j++)
            {
                var next = rnd.Next(srcCount);
                while (next == curSlots[j]) next = rnd.Next(srcCount);

                var leaving = centered[curSlots[j]];
                var entering = centered[next];
                for (var d = 0; d < dimNum; d++)
                {
                    nextPoint[d] = curPoint[d] - leaving[d] + entering[d];
                }
                var nextDist = SquareSum(nextPoint);
                if (nextDist < curDist)
                {
                    curSlots[j] = next;
                    Array.Copy(nextPoint, curPoint, dimNum);
                    curDist = nextDist;
                }
            }
        }

        for (var s = 0; s < slots; s++) scores[curSlots[s]]++;

        var ranking = new List<int[]>();
        for (var i = 0; i < srcCount; i++)
        {
            if (scores[i] > 0) ranking.Add(new[] { i, (int)scores[i] });
        }
        ranking.Sort((a, b) => b[1].CompareTo(a[1]));

        var intDist = (long)Math.Round(IntegerScale * curDist);
        long prevDist;
        do
        {
            ct.ThrowIfCancellationRequested();
            prevDist = intDist;
            for (var i = ranking.Count - 1; i >= 0; i--)
            {
                if (ranking[i][1] <= 0) continue;
                for (var j = 0; j < ranking.Count; j++)
                {
                    if (i == j) continue;
                    var leaving = centered[ranking[i][0]];
                    var entering = centered[ranking[j][0]];
                    for (var d = 0; d < dimNum; d++)
                    {
                        nextPoint[d] = curPoint[d] - leaving[d] + entering[d];
                    }
                    var nd = (long)Math.Round(IntegerScale * SquareSum(nextPoint));
                    if (nd < intDist)
                    {
                        ranking[i][1]--;
                        ranking[j][1]++;
                        Array.Copy(nextPoint, curPoint, dimNum);
                        intDist = nd;
                        break;
                    }
                }
            }
        } while (intDist < prevDist);

        var finalScores = new double[srcCount];
        foreach (var entry in ranking)
        {
            finalScores[entry[0]] = (double)entry[1] / slots;
        }

        curDist = SquareSum(curPoint);
        return new FmcResult(Math.Round(Math.Sqrt(curDist), 8), finalScores);
    }

    private static double[] Subtract(double[] a, double[] b)
    {
        var len = a.Length;
        var r = new double[len];
        for (var i = 0; i < len; i++) r[i] = a[i] - b[i];
        return r;
    }

    private static double SquareSum(double[] arr)
    {
        double s = 0;
        for (var i = 0; i < arr.Length; i++) s += arr[i] * arr[i];
        return s;
    }

    private static double[] ScaleBySlots(double[] values, int slots)
    {
        var r = new double[values.Length];
        for (var i = 0; i < values.Length; i++) r[i] = values[i] / slots;
        return r;
    }

    private static double[][] ScaleSourceBySlots(IReadOnlyList<G25CoordinateParser.CoordinateRow> source, int slots)
    {
        var r = new double[source.Count][];
        for (var i = 0; i < source.Count; i++) r[i] = ScaleBySlots(source[i].Values, slots);
        return r;
    }
}
