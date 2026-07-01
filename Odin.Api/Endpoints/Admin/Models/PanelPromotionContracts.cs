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

/// <summary>
/// A self-contained, environment-portable promotion bundle exported from one environment (dev) and
/// applied to another (prod): the panel's sample→population <see cref="Links"/> (keyed by population
/// <i>name</i>, since ids differ per environment) plus every sample's <see cref="Labels"/> (keyed by
/// sample id). One downloadable file; the unit the "Export snapshot" / "Apply snapshot" buttons move.
/// </summary>
public sealed class PanelPromotionBundle
{
    public string Panel { get; set; } = "HO";

    /// <summary>ISO-8601 UTC timestamp of the export (informational).</summary>
    public string? ExportedAtUtc { get; set; }

    public PanelLinksSnapshot Links { get; set; } = new();
    public PanelLabelsSnapshot Labels { get; set; } = new();
}
