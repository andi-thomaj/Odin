using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Endpoints.Admin.Models;
using Odin.Api.Endpoints.MergeManagement;

namespace Odin.Api.Endpoints.Admin;

/// <summary>
/// Runtime engine behind the Panel Labels "Export snapshot" / "Apply snapshot" buttons — the dev→prod
/// promotion the seeder previously did only on deploy. <see cref="ExportAsync"/> snapshots the CURRENT
/// environment (DB links denormalised to population <i>name</i> + the live <c>.ind</c> labels) into a
/// portable <see cref="PanelPromotionBundle"/>; <see cref="ApplyAsync"/> applies a bundle onto the
/// current environment (dry-run for a preview, or for real). It reuses
/// <see cref="PanelPromotionSnapshots.ApplyLinksMirrorAsync"/> + <see cref="PanelPromotionSnapshots.ApplyLabelsAsync"/>
/// so the runtime path and the startup seeder converge identically.
/// </summary>
public interface IPanelPromotionService
{
    Task<PanelPromotionBundle> ExportAsync(string? panel, CancellationToken cancellationToken = default);

    Task<PanelPromotionApplyResult> ApplyAsync(
        PanelPromotionBundle bundle, string identityId, bool dryRun,
        CancellationToken cancellationToken = default);
}

public sealed class PanelPromotionService(ApplicationDbContext db, IMergePipelineService merge)
    : IPanelPromotionService
{
    private const string DefaultPanel = "HO";

    public async Task<PanelPromotionBundle> ExportAsync(
        string? panel, CancellationToken cancellationToken = default)
    {
        var p = Normalize(panel);

        // Links from the DB, denormalised to the population NAME so the bundle is portable to an
        // environment whose numeric population ids differ (prod's ids != dev's for the same name).
        var links = await db.QpadmPopulationPanelSamples.AsNoTracking()
            .Where(l => l.Panel == p)
            .Join(
                db.QpadmPopulations.AsNoTracking(),
                l => l.QpadmPopulationId,
                pop => pop.Id,
                (l, pop) => new PanelLinkRow { Panel = l.Panel, SampleId = l.SampleId, PopulationName = pop.Name })
            .OrderBy(r => r.SampleId).ThenBy(r => r.PopulationName)
            .ToListAsync(cancellationToken);

        // Labels from the live .ind (via tools-api), keyed by the stable sample id.
        var ind = await merge.GetPanelIndRowsAsync(p, cancellationToken);
        var labels = new PanelLabelsSnapshot
        {
            Panel = p,
            Rows = ind.Rows.Select(r => new PanelLabelRow { Id = r.Id, Label = r.Label }).ToList(),
        };

        return new PanelPromotionBundle
        {
            Panel = p,
            ExportedAtUtc = DateTime.UtcNow.ToString("O"),
            Links = new PanelLinksSnapshot { Panels = [p], Links = links },
            Labels = labels,
        };
    }

    public async Task<PanelPromotionApplyResult> ApplyAsync(
        PanelPromotionBundle bundle, string identityId, bool dryRun,
        CancellationToken cancellationToken = default)
    {
        var p = Normalize(bundle.Panel);

        var linksSnapshot = bundle.Links ?? new PanelLinksSnapshot();
        // A bundle is always authoritative for its own panel, so the full mirror runs even if the
        // exporter didn't set Panels (without this the mirror would be a silent no-op).
        if (linksSnapshot.Panels is null || linksSnapshot.Panels.Count == 0)
            linksSnapshot.Panels = [p];

        var labelsSnapshot = bundle.Labels ?? new PanelLabelsSnapshot { Panel = p };

        // Links are transactional (one SaveChanges); labels are best-effort against the tools-api
        // .ind (with its own .ind.bak) and never throw — a tools-api hiccup surfaces in Labels.Error,
        // and the whole apply is idempotent, so re-clicking reconciles. dryRun writes nothing.
        var linksResult = await PanelPromotionSnapshots.ApplyLinksMirrorAsync(
            db, linksSnapshot, identityId, dryRun, cancellationToken);
        var labelsResult = await PanelPromotionSnapshots.ApplyLabelsAsync(
            merge, labelsSnapshot, dryRun, cancellationToken);

        return new PanelPromotionApplyResult
        {
            DryRun = dryRun,
            Panel = p,
            Links = linksResult,
            Labels = labelsResult,
        };
    }

    private static string Normalize(string? panel) =>
        string.IsNullOrWhiteSpace(panel) ? DefaultPanel : panel.Trim();
}
