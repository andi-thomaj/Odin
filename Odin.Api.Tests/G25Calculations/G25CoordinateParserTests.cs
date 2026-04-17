using Odin.Api.Endpoints.G25Calculations;

namespace Odin.Api.Tests.G25Calculations;

public class G25CoordinateParserTests
{
    [Fact]
    public void Parse_ValidSingleRow_ReturnsParsedRow()
    {
        var text = "PopA,0.1,0.2,0.3";

        var result = G25CoordinateParser.Parse(text, "SOURCE");

        Assert.Equal(0, result.Errors);
        Assert.NotNull(result.Lines);
        Assert.Single(result.Lines);
        Assert.Equal("PopA", result.Lines[0].Name);
        Assert.Equal(new[] { 0.1, 0.2, 0.3 }, result.Lines[0].Values);
    }

    [Fact]
    public void Parse_ValidMultipleRows_ReturnsAllRows()
    {
        var text = "A,0.1,0.2\nB,0.3,0.4\nC,0.5,0.6";

        var result = G25CoordinateParser.Parse(text, "SOURCE");

        Assert.Equal(0, result.Errors);
        Assert.NotNull(result.Lines);
        Assert.Equal(3, result.Lines.Count);
        Assert.Equal("B", result.Lines[1].Name);
        Assert.Equal(new[] { 0.3, 0.4 }, result.Lines[1].Values);
    }

    [Fact]
    public void Parse_CarriageReturnLineFeedNormalizedToLineFeed()
    {
        var text = "A,0.1,0.2\r\nB,0.3,0.4";

        var result = G25CoordinateParser.Parse(text, "SOURCE");

        Assert.Equal(0, result.Errors);
        Assert.NotNull(result.Lines);
        Assert.Equal(2, result.Lines.Count);
    }

    [Fact]
    public void Parse_NameWithSpaces_ReplacesWithGreaterThan()
    {
        var text = "Ancient Pop One,0.1,0.2";

        var result = G25CoordinateParser.Parse(text, "SOURCE");

        Assert.Equal(0, result.Errors);
        Assert.NotNull(result.Lines);
        Assert.Equal("Ancient>Pop>One", result.Lines[0].Name);
    }

    [Fact]
    public void Parse_DoubleQuotesRemoved()
    {
        var text = "\"PopA\",0.1,0.2";

        var result = G25CoordinateParser.Parse(text, "SOURCE");

        Assert.Equal(0, result.Errors);
        Assert.NotNull(result.Lines);
        Assert.Equal("PopA", result.Lines[0].Name);
    }

    [Fact]
    public void Parse_TabWhitespaceTriggersWarningAndIsStripped()
    {
        var text = "PopA,\t0.1,0.2";

        var result = G25CoordinateParser.Parse(text, "SOURCE");

        Assert.Equal(0, result.Errors);
        Assert.Contains("WARNING! Whitespace removed", result.Message);
        Assert.NotNull(result.Lines);
        Assert.Equal(new[] { 0.1, 0.2 }, result.Lines[0].Values);
    }

    [Fact]
    public void Parse_EmptyLinesBetweenRowsTriggersWarningAndCollapses()
    {
        var text = "A,0.1,0.2\n\n\nB,0.3,0.4";

        var result = G25CoordinateParser.Parse(text, "SOURCE");

        Assert.Equal(0, result.Errors);
        Assert.Contains("WARNING! Empty lines removed", result.Message);
        Assert.NotNull(result.Lines);
        Assert.Equal(2, result.Lines.Count);
    }

    [Fact]
    public void Parse_ConsecutiveCommas_ReturnsMissingValuesError()
    {
        var text = "A,0.1,,0.3";

        var result = G25CoordinateParser.Parse(text, "TARGET");

        Assert.Equal(1, result.Errors);
        Assert.Null(result.Lines);
        Assert.Contains("ERROR! Missing values in TARGET", result.Message);
    }

    [Fact]
    public void Parse_EmptyString_ReturnsEmptyError()
    {
        var result = G25CoordinateParser.Parse("   ", "SOURCE");

        Assert.Equal(1, result.Errors);
        Assert.Null(result.Lines);
        Assert.Contains("ERROR! Empty SOURCE", result.Message);
    }

    [Fact]
    public void Parse_SingleColumn_ReturnsDataLoadError()
    {
        var text = "JustNameNoValues";

        var result = G25CoordinateParser.Parse(text, "SOURCE");

        Assert.Equal(1, result.Errors);
        Assert.Null(result.Lines);
        Assert.Contains("ERROR! Data load error in SOURCE", result.Message);
    }

    [Fact]
    public void Parse_VariableColumnCount_ReturnsError()
    {
        var text = "A,0.1,0.2\nB,0.3";

        var result = G25CoordinateParser.Parse(text, "TARGET");

        Assert.Equal(1, result.Errors);
        Assert.Null(result.Lines);
        Assert.Contains("ERROR! Variable column number in TARGET", result.Message);
    }

    [Fact]
    public void Parse_NonNumericValue_ReturnsError()
    {
        var text = "A,0.1,foo";

        var result = G25CoordinateParser.Parse(text, "SOURCE");

        Assert.Equal(1, result.Errors);
        Assert.Null(result.Lines);
        Assert.Contains("ERROR! Non-numerical value in SOURCE", result.Message);
    }

    [Fact]
    public void Parse_ParsesNegativeAndScientific()
    {
        var text = "A,-0.0123,1.5e-2";

        var result = G25CoordinateParser.Parse(text, "SOURCE");

        Assert.Equal(0, result.Errors);
        Assert.NotNull(result.Lines);
        Assert.Equal(-0.0123, result.Lines[0].Values[0], 10);
        Assert.Equal(0.015, result.Lines[0].Values[1], 10);
    }

    [Fact]
    public void Parse_LeadingAndTrailingWhitespace_Trimmed()
    {
        var text = "\n\n   A,0.1,0.2   \n\n";

        var result = G25CoordinateParser.Parse(text, "SOURCE");

        Assert.Equal(0, result.Errors);
        Assert.NotNull(result.Lines);
        Assert.Single(result.Lines);
        Assert.Equal("A", result.Lines[0].Name);
    }

    [Fact]
    public void Parse_ColumnLengthPropertyIncludesNameColumn()
    {
        var text = "A,0.1,0.2,0.3";

        var result = G25CoordinateParser.Parse(text, "SOURCE");

        Assert.Equal(0, result.Errors);
        Assert.NotNull(result.Lines);
        Assert.Equal(4, result.Lines[0].Length);
    }
}
