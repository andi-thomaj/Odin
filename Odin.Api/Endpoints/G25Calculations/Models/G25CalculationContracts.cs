namespace Odin.Api.Endpoints.G25Calculations.Models;

public static class ComputeDistancesContract
{
    public class Request
    {
        public required string TargetCoordinates { get; set; }
        public string? SourceContent { get; set; }
        public int? SourceDistanceFileId { get; set; }
        public int? G25DistanceEraId { get; set; }
        public int? MaxResults { get; set; }
    }

    public class Response
    {
        public required IReadOnlyList<DistanceTargetResult> Results { get; set; }
    }

    public class DistanceTargetResult
    {
        public required string TargetName { get; set; }
        public required IReadOnlyList<DistanceRow> Rows { get; set; }
    }

    public class DistanceRow
    {
        public required string Name { get; set; }
        public double Distance { get; set; }
    }
}

public static class ComputeAdmixtureSingleContract
{
    public class Request
    {
        public required string TargetCoordinates { get; set; }
        public string? SourceContent { get; set; }
        public int? SourceAdmixtureFileId { get; set; }
        public IReadOnlyList<int>? SourceRegionIds { get; set; }
        public double? CyclesMultiplier { get; set; }
        public int? Slots { get; set; }
        public bool? Aggregate { get; set; }
        public bool? PrintZeroes { get; set; }
    }

    public class Response
    {
        public required IReadOnlyList<AdmixtureSingleResult> Results { get; set; }
    }

    public class AdmixtureSingleResult
    {
        public required string TargetName { get; set; }
        public double Distance { get; set; }
        public required string DistancePct { get; set; }
        public required IReadOnlyList<AdmixtureRow> Rows { get; set; }
    }

    public class AdmixtureRow
    {
        public required string Name { get; set; }
        public double Pct { get; set; }
    }
}

public static class ComputeAdmixtureMultiContract
{
    public class Request
    {
        public required string TargetCoordinates { get; set; }
        public required string SourceContent { get; set; }
        public double? CyclesMultiplier { get; set; }
        public bool? FastMode { get; set; }
        public bool? Aggregate { get; set; }
        public bool? PrintZeroes { get; set; }
    }

    public class Response
    {
        public required IReadOnlyList<string> SourceNames { get; set; }
        public required IReadOnlyList<AdmixtureMultiTarget> Targets { get; set; }
        public double AverageDistance { get; set; }
        public required IReadOnlyList<double> AverageScores { get; set; }
    }

    public class AdmixtureMultiTarget
    {
        public required string Name { get; set; }
        public double Distance { get; set; }
        public required IReadOnlyList<double> Scores { get; set; }
    }
}
