using System.Globalization;
using Odin.Api.Data.Seeders;

namespace Odin.Api.Tests.Seeders;

public class G25PcaSeedBuilderTests
{
    private static string Coords(int start) =>
        string.Join(',', Enumerable.Range(start, 25).Select(i => (i * 0.001).ToString(CultureInfo.InvariantCulture)));

    [Fact]
    public void ParseFileRows_SkipsHeaderAndMalformed_AndSplitsLabels()
    {
        var lines = new[]
        {
            "," + string.Join(',', Enumerable.Range(1, 25).Select(i => $"PC{i}")), // Modern-style header
            $"PopA.AG:I100.AG,{Coords(1)}",
            $"Bad:Row,1,2,3", // only 3 PC values -> malformed
            "",
            $"NoColonPop,{Coords(2)}",
        };

        var rows = G25PcaSeedBuilder.ParseFileRows(lines);

        Assert.Equal(2, rows.Count);

        Assert.Equal("I100.AG", rows[0].IndividualPortion);
        Assert.Equal("PopA.AG", rows[0].PopulationPrefix);

        // No colon -> the whole label is both population and individual.
        Assert.Equal("NoColonPop", rows[1].IndividualPortion);
        Assert.Equal("NoColonPop", rows[1].PopulationPrefix);
    }

    [Fact]
    public void Build_AggregatesEachPopulation_Prefixmulti_Dirty_Unmatched_Modern_AndDedup()
    {
        var coordsAg = Coords(1);
        var coordsSg = Coords(2);
        var coordsI200 = Coords(3);
        var coordsM1 = Coords(4);
        var coordsM2 = Coords(5);

        var eraRows = new Dictionary<int, List<G25CoordinateFileRow>>
        {
            [1] =
            [
                new G25CoordinateFileRow("I100.AG__BC_1", "PopA.AG", coordsAg),
                new G25CoordinateFileRow("I100.SG__BC_1", "PopA.SG", coordsSg),
                new G25CoordinateFileRow("I200", "PopB", coordsI200),
            ],
            [G25PcaSeedBuilder.ModernEraId] =
            [
                new G25CoordinateFileRow("M1", "ModPop", coordsM1),
                new G25CoordinateFileRow("M2", "ModPop", coordsM2),
            ],
        };

        var distance = new List<G25PcaDistanceSample>
        {
            // I100 -> boundary-prefix hits BOTH .AG and .SG (two members); I200 -> exact (one member);
            // I999 -> unmatched; "0.123" -> dirty (decimal); "foo:bar" -> dirty (colon).
            new("Alpha", "I100,I200,I999,0.123,foo:bar", 1),
            new("Alpha", "I200", 1), // second Alpha sample in era 1 -> merges; its I200 is a duplicate member
            new("ModThing", "ignored", G25PcaSeedBuilder.ModernEraId), // modern distance ids are ignored
        };

        var result = G25PcaSeedBuilder.Build(distance, eraRows);

        // One record PER POPULATION now: a single "Alpha" cluster (both Alpha samples merged) + one
        // "ModPop" cluster.
        Assert.Equal(1, result.AncientClusters);
        Assert.Equal(1, result.ModernClusters);
        Assert.Equal(3, result.AncientMembers);
        Assert.Equal(2, result.ModernMembers);
        Assert.Equal(2, result.Records.Count);
        Assert.Equal(1, result.DedupSkipped); // the second Alpha's I200

        Assert.Single(result.Unmatched);
        Assert.Equal("I999", result.Unmatched[0].Token);

        Assert.Equal(2, result.Dirty.Count);
        Assert.Contains(result.Dirty, d => d.Token == "0.123");
        Assert.Contains(result.Dirty, d => d.Token == "foo:bar");

        // Alpha aggregates all three matched members (prefix-multi .AG + .SG + the exact I200), members
        // sorted by individual portion, coordinates ';'-joined and ids ','-joined in the same order.
        var alpha = Assert.Single(result.Records, r => r is { Label: "Alpha", EraId: 1 });
        Assert.Equal("I100.AG__BC_1,I100.SG__BC_1,I200", alpha.Ids);
        Assert.Equal($"{coordsAg};{coordsSg};{coordsI200}", alpha.Coordinates);

        // Modern is grouped by the file's population prefix (distance ids ignored): one "ModPop" cluster.
        var modPop = Assert.Single(result.Records, r => r.EraId == G25PcaSeedBuilder.ModernEraId);
        Assert.Equal("ModPop", modPop.Label);
        Assert.Equal("M1,M2", modPop.Ids);
        Assert.Equal($"{coordsM1};{coordsM2}", modPop.Coordinates);
    }
}
