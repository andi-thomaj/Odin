namespace Odin.Api.Endpoints.Admin.Models;

/// <summary>
/// One sample → population link, keyed by population <b>Name</b> (not Id) so the snapshot is
/// portable across environments whose numeric ids differ.
/// </summary>
public sealed class PanelLinkRow
{
    public string Panel { get; set; } = string.Empty;
    public string SampleId { get; set; } = string.Empty;
    public string PopulationName { get; set; } = string.Empty;
}

/// <summary>
/// Committed snapshot of sample→population links. <see cref="Panels"/> lists the panels the snapshot
/// is authoritative for, so a full-mirror knows to clear a panel even when it has zero links.
/// </summary>
public sealed class PanelLinksSnapshot
{
    public List<string> Panels { get; set; } = [];
    public List<PanelLinkRow> Links { get; set; } = [];
}

/// <summary>One panel sample's label, keyed by the stable <c>.ind</c> sample id.</summary>
public sealed class PanelLabelRow
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
}

/// <summary>Committed snapshot of every panel sample's label (the git diff is the changelog).</summary>
public sealed class PanelLabelsSnapshot
{
    public string Panel { get; set; } = "HO";
    public List<PanelLabelRow> Rows { get; set; } = [];
}

public static class PanelPromotionImportContract
{
    /// <summary>Summary of applying the committed snapshots to the current environment.</summary>
    public class Response
    {
        public int LinksAdded { get; set; }
        public int LinksRemoved { get; set; }
        public int LinksUnchanged { get; set; }
        public List<string> UnknownPopulations { get; set; } = [];

        public bool LabelsApplied { get; set; }
        public int LabelsChanged { get; set; }
        public int LabelsTotal { get; set; }
        public List<string> MissingSamples { get; set; } = [];
        /// <summary>Non-null when the label apply step failed (e.g. tools-api unreachable).</summary>
        public string? LabelsError { get; set; }

        public long DurationMs { get; set; }
    }
}
