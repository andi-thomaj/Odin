using Odin.Api.Endpoints.G25Calculations;

namespace Odin.Api.Tests.G25Calculations;

public class G25AdmixtureSolverTests
{
    private static G25CoordinateParser.CoordinateRow Row(string name, params double[] values) =>
        new() { Name = name, Values = values };

    [Fact]
    public void ComputeDistances_ProducesExpectedOrderingAndValues()
    {
        var source = new[]
        {
            Row("Far", 1.0, 0.0),
            Row("Near", 0.01, 0.0),
            Row("Mid", 0.1, 0.0)
        };
        var target = new[] { Row("T", 0.0, 0.0) };

        var result = G25AdmixtureSolver.ComputeDistances(source, target, 0, 10);

        Assert.Equal("T", result.TargetName);
        Assert.Equal(3, result.Rows.Count);
        Assert.Equal("Near", result.Rows[0].Name);
        Assert.Equal("Mid", result.Rows[1].Name);
        Assert.Equal("Far", result.Rows[2].Name);
        Assert.Equal(0.01, result.Rows[0].Distance, 10);
        Assert.Equal(0.1, result.Rows[1].Distance, 10);
        Assert.Equal(1.0, result.Rows[2].Distance, 10);
    }

    [Fact]
    public void ComputeDistances_MaxOutLimitsOutput()
    {
        var source = new[]
        {
            Row("A", 1.0, 0.0),
            Row("B", 2.0, 0.0),
            Row("C", 3.0, 0.0)
        };
        var target = new[] { Row("T", 0.0, 0.0) };

        var result = G25AdmixtureSolver.ComputeDistances(source, target, 0, 2);

        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("A", result.Rows[0].Name);
        Assert.Equal("B", result.Rows[1].Name);
    }

    [Fact]
    public void ComputeDistances_SkipsSourceMatchingTargetName()
    {
        var source = new[]
        {
            Row("T", 0.0, 0.0),
            Row("Other", 1.0, 1.0)
        };
        var target = new[] { Row("T", 0.0, 0.0) };

        var result = G25AdmixtureSolver.ComputeDistances(source, target, 0, 10);

        Assert.Single(result.Rows);
        Assert.Equal("Other", result.Rows[0].Name);
    }

    [Fact]
    public void ComputeSingle_SingleSource_AssignsHundredPercent()
    {
        var source = new[] { Row("OnlyPop", 0.3, 0.4) };
        var target = new[] { Row("T", 0.3, 0.4) };

        var result = G25AdmixtureSolver.ComputeSingle(
            source, target, 0, cyclesMultiplier: 1.0, slots: 500, aggregate: false, printZeroes: false,
            CancellationToken.None);

        Assert.Equal("T", result.TargetName);
        Assert.Single(result.Rows);
        Assert.Equal("OnlyPop", result.Rows[0].Name);
        Assert.Equal(100.0, result.Rows[0].Pct, 8);
    }

    [Fact]
    public void ComputeSingle_ScoresSumToOneHundredPercent()
    {
        var rnd = new Random(42);
        const int dims = 8;
        var source = new G25CoordinateParser.CoordinateRow[6];
        for (var i = 0; i < source.Length; i++)
        {
            var v = new double[dims];
            for (var d = 0; d < dims; d++) v[d] = rnd.NextDouble();
            source[i] = Row($"S{i}", v);
        }
        var tgtVals = new double[dims];
        for (var d = 0; d < dims; d++) tgtVals[d] = rnd.NextDouble();
        var target = new[] { Row("T", tgtVals) };

        var result = G25AdmixtureSolver.ComputeSingle(
            source, target, 0, cyclesMultiplier: 1.0, slots: 500, aggregate: false, printZeroes: true,
            CancellationToken.None);

        var sum = result.Rows.Sum(r => r.Pct);
        Assert.InRange(sum, 99.999, 100.001);
        Assert.True(result.Distance >= 0);
    }

    [Fact]
    public void ComputeSingle_RecoversKnownMixture_TopTwoDominate()
    {
        const int dims = 12;
        var rnd = new Random(7);
        var a = new double[dims];
        var b = new double[dims];
        for (var d = 0; d < dims; d++)
        {
            a[d] = rnd.NextDouble();
            b[d] = rnd.NextDouble();
        }

        var distractors = new G25CoordinateParser.CoordinateRow[6];
        for (var i = 0; i < distractors.Length; i++)
        {
            var v = new double[dims];
            for (var d = 0; d < dims; d++) v[d] = rnd.NextDouble() + 5.0;
            distractors[i] = Row($"Far{i}", v);
        }

        var source = new[] { Row("A", a), Row("B", b) }
            .Concat(distractors)
            .ToArray();

        var tgtVals = new double[dims];
        for (var d = 0; d < dims; d++) tgtVals[d] = 0.5 * a[d] + 0.5 * b[d];
        var target = new[] { Row("T", tgtVals) };

        double totalTopTwoPct = 0;
        const int runs = 5;
        for (var run = 0; run < runs; run++)
        {
            var result = G25AdmixtureSolver.ComputeSingle(
                source, target, 0, cyclesMultiplier: 1.0, slots: 500, aggregate: false, printZeroes: false,
                CancellationToken.None);

            var byName = result.Rows.ToDictionary(r => r.Name, r => r.Pct);
            totalTopTwoPct += byName.GetValueOrDefault("A", 0) + byName.GetValueOrDefault("B", 0);
        }

        var avgAB = totalTopTwoPct / runs;
        Assert.True(avgAB > 95.0, $"Expected A+B to average >95%, got {avgAB:F2}%");
    }

    [Fact]
    public void ComputeSingle_PrintZeroesFalse_OmitsZeroScores()
    {
        var source = new[]
        {
            Row("Match", 0.3, 0.4),
            Row("Far", 100.0, 100.0)
        };
        var target = new[] { Row("T", 0.3, 0.4) };

        var result = G25AdmixtureSolver.ComputeSingle(
            source, target, 0, cyclesMultiplier: 1.0, slots: 500, aggregate: false, printZeroes: false,
            CancellationToken.None);

        Assert.DoesNotContain(result.Rows, r => r.Pct == 0);
    }

    [Fact]
    public void ComputeSingle_AggregateGroupsByPrefixBeforeColon()
    {
        var source = new[]
        {
            Row("Group:Sub1", 0.3, 0.4),
            Row("Group:Sub2", 0.31, 0.41),
            Row("Far", 100.0, 100.0)
        };
        var target = new[] { Row("T", 0.3, 0.4) };

        var result = G25AdmixtureSolver.ComputeSingle(
            source, target, 0, cyclesMultiplier: 1.0, slots: 500, aggregate: true, printZeroes: true,
            CancellationToken.None);

        Assert.Contains(result.Rows, r => r.Name == "Group");
        Assert.DoesNotContain(result.Rows, r => r.Name.Contains(':'));
    }

    [Fact]
    public void ComputeMulti_AverageDistanceIsMeanOfPerTargetDistances()
    {
        var source = new[]
        {
            Row("A", 0.0, 0.0),
            Row("B", 1.0, 0.0),
            Row("C", 0.0, 1.0)
        };
        var target = new[]
        {
            Row("T1", 0.5, 0.0),
            Row("T2", 0.0, 0.5)
        };

        var result = G25AdmixtureSolver.ComputeMulti(
            source, target, cyclesMultiplier: 1.0, fastMode: false, aggregate: false, printZeroes: true,
            CancellationToken.None);

        Assert.Equal(2, result.Targets.Count);
        var mean = (result.Targets[0].Distance + result.Targets[1].Distance) / 2.0;
        Assert.Equal(mean, result.AverageDistance, 8);
    }

    [Fact]
    public void ComputeMulti_ScoresPerTargetSumCloseToHundred()
    {
        var source = new[]
        {
            Row("A", 0.0, 0.0),
            Row("B", 1.0, 0.0),
            Row("C", 0.0, 1.0)
        };
        var target = new[]
        {
            Row("T1", 0.33, 0.33),
            Row("T2", 0.5, 0.0)
        };

        var result = G25AdmixtureSolver.ComputeMulti(
            source, target, cyclesMultiplier: 1.0, fastMode: false, aggregate: false, printZeroes: true,
            CancellationToken.None);

        foreach (var t in result.Targets)
        {
            var sum = t.Scores.Sum();
            Assert.InRange(sum, 99.999, 100.001);
        }
    }

    [Fact]
    public void ComputeSingle_RespectsCancellation()
    {
        var source = new G25CoordinateParser.CoordinateRow[40];
        var rnd = new Random(1);
        for (var i = 0; i < source.Length; i++)
        {
            var v = new double[25];
            for (var d = 0; d < 25; d++) v[d] = rnd.NextDouble();
            source[i] = Row($"S{i}", v);
        }
        var tgtVals = new double[25];
        for (var d = 0; d < 25; d++) tgtVals[d] = rnd.NextDouble();
        var target = new[] { Row("T", tgtVals) };

        var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.Throws<OperationCanceledException>(() =>
            G25AdmixtureSolver.ComputeSingle(
                source, target, 0, cyclesMultiplier: 8.0, slots: 500, aggregate: false, printZeroes: true,
                cts.Token));
    }
}
