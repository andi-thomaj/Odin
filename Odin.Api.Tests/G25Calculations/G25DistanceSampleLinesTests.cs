using Odin.Api.Endpoints.G25Calculations;

namespace Odin.Api.Tests.G25Calculations;

public class G25DistanceSampleLinesTests
{
    // Reference data can contain the odd malformed row (a DOI/URL or a paper title pasted into the
    // Coordinates field). One such row must NOT make the parser reject the whole era — these tests pin
    // that a malformed sample is dropped (and reported) while the good ones are kept and normalised.
    [Fact]
    public void SelectValidSampleLines_DropsMalformed_KeepsGoodOnes_Normalised()
    {
        var samples = new (string Label, string Coordinates)[]
        {
            ("Good Plain", "0.10,0.20,0.30"),          // bare numeric coords
            ("Good Named", "Tech_Named,0.40,0.50,0.60"), // leading source-name prefix (stripped by BuildSampleLine)
            ("Bad URL", "https://doi.org/10.1371/journal.pone.0350298"), // citation URL, no coordinates
            ("Bad Title", "Bioarchaeological evidence of an Islamic burial"), // paper title, no coordinates
            ("Bad Value", "Tech_Bad,0.1,notanumber,0.3"),  // a non-numeric value
        };

        var (lines, skipped) = G25CalculationService.SelectValidSampleLines(samples);

        Assert.Equal(
            new[] { "Good Plain,0.10,0.20,0.30", "Good Named,0.40,0.50,0.60" },
            lines);

        Assert.Contains("Bad URL", skipped);
        Assert.Contains("Bad Title", skipped);
        Assert.Contains("Bad Value", skipped);
        Assert.Equal(3, skipped.Count);
    }

    [Fact]
    public void SelectValidSampleLines_DropsLengthOutliers_ToKeepAConsistentWidth()
    {
        // Majority have 3 values; the odd one with 2 must be dropped so the parser doesn't fail on a
        // variable column count — and it should be reported as skipped, not silently discarded.
        var samples = new (string Label, string Coordinates)[]
        {
            ("A", "0.1,0.2,0.3"),
            ("B", "0.4,0.5,0.6"),
            ("Short", "0.7,0.8"),
        };

        var (lines, skipped) = G25CalculationService.SelectValidSampleLines(samples);

        Assert.Equal(new[] { "A,0.1,0.2,0.3", "B,0.4,0.5,0.6" }, lines);
        Assert.Equal(new[] { "Short" }, skipped);
    }

    [Fact]
    public void SelectValidSampleLines_AllClean_KeepsEverything()
    {
        var samples = new (string Label, string Coordinates)[]
        {
            ("A", "0.1,0.2"),
            ("B", "Name_B,0.3,0.4"),
        };

        var (lines, skipped) = G25CalculationService.SelectValidSampleLines(samples);

        Assert.Equal(new[] { "A,0.1,0.2", "B,0.3,0.4" }, lines);
        Assert.Empty(skipped);
    }
}
