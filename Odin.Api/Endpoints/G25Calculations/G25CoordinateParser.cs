using System.Globalization;
using System.Text.RegularExpressions;

namespace Odin.Api.Endpoints.G25Calculations;

public static class G25CoordinateParser
{
    public sealed class ParseResult
    {
        public IReadOnlyList<CoordinateRow>? Lines { get; set; }
        public string Message { get; set; } = string.Empty;
        public int Errors { get; set; }
    }

    public sealed class CoordinateRow
    {
        public required string Name { get; set; }
        public required double[] Values { get; set; }

        public int Length => Values.Length + 1;
    }

    private static readonly Regex NonNewlineNonSpaceWhitespace = new("[^\\S\n ]", RegexOptions.Compiled);
    private static readonly Regex EmptyLines = new("\n+", RegexOptions.Compiled);
    private static readonly Regex MultipleCommas = new(",+", RegexOptions.Compiled);

    public static ParseResult Parse(string text, string label)
    {
        var result = new ParseResult { Message = string.Empty, Errors = 0, Lines = Array.Empty<CoordinateRow>() };

        var cleaned = (text ?? string.Empty).Trim().Replace("\r\n", "\n").Replace("\"", string.Empty);

        var afterWs = NonNewlineNonSpaceWhitespace.Replace(cleaned, string.Empty);
        var wsDiff = cleaned.Length - afterWs.Length;
        if (wsDiff > 0)
        {
            result.Message += $"WARNING! Whitespace removed in {label}: {wsDiff}. ";
        }
        cleaned = afterWs;

        var afterEmpty = EmptyLines.Replace(cleaned, "\n");
        var emptyDiff = cleaned.Length - afterEmpty.Length;
        if (emptyDiff > 0)
        {
            result.Message += $"WARNING! Empty lines removed in {label}: {emptyDiff}. ";
        }
        cleaned = afterEmpty;

        var afterMissing = MultipleCommas.Replace(cleaned, ",");
        if (cleaned.Length - afterMissing.Length > 0)
        {
            result.Message += $"ERROR! Missing values in {label}. ";
            result.Errors = 1;
            result.Lines = null;
            return result;
        }

        if (string.IsNullOrEmpty(cleaned))
        {
            result.Message += $"ERROR! Empty {label}. ";
            result.Errors = 1;
            result.Lines = null;
            return result;
        }

        var rawLines = cleaned.Split('\n');
        var firstCols = rawLines[0].Split(',');
        var colNum = firstCols.Length;
        if (colNum == 1)
        {
            result.Message += $"ERROR! Data load error in {label}. ";
            result.Errors = 1;
            result.Lines = null;
            return result;
        }

        var parsed = new List<CoordinateRow>(rawLines.Length);
        foreach (var raw in rawLines)
        {
            var cols = raw.Split(',');
            if (cols.Length != colNum)
            {
                result.Message += $"ERROR! Variable column number in {label}. ";
                result.Errors = 1;
                result.Lines = null;
                return result;
            }

            var values = new double[cols.Length - 1];
            for (var j = 1; j < cols.Length; j++)
            {
                var v = cols[j].Trim();
                if (string.IsNullOrEmpty(v) || !double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var num))
                {
                    result.Message += $"ERROR! Non-numerical value in {label}. ";
                    result.Errors = 1;
                    result.Lines = null;
                    return result;
                }
                values[j - 1] = num;
            }

            parsed.Add(new CoordinateRow
            {
                Name = cols[0].Trim().Replace(' ', '>'),
                Values = values
            });
        }

        result.Lines = parsed;
        return result;
    }
}
