using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Endpoints.Admin.Models;
using Odin.Api.Endpoints.MergeManagement;

namespace Odin.Api.Endpoints.Admin;

public interface IPanelPromotionService
{
    /// <summary>Snapshot the current environment's links (from DB) + labels (from tools-api) for download.</summary>
    Task<PanelPromotionExportContract.Response> ExportAsync(string panel, CancellationToken cancellationToken = default);

    /// <summary>Apply the committed snapshot files onto the current environment: mirror links, then
    /// diff &amp; apply changed labels via tools-api.</summary>
    Task<PanelPromotionImportContract.Response> ImportAsync(string identityId, CancellationToken cancellationToken = default);

    /// <summary>Diff a labels snapshot against the live <c>.ind</c> and write only the changed rows.
    /// Exposed for the import flow and tests; never throws — failures surface in the result.</summary>
    Task<LabelApplyResult> ApplyLabelsAsync(PanelLabelsSnapshot snapshot, CancellationToken cancellationToken = default);
}

/// <summary>Outcome of applying a labels snapshot to the live panel <c>.ind</c>.</summary>
public sealed class LabelApplyResult
{
    public bool Applied { get; set; }
    public int Changed { get; set; }
    public int Total { get; set; }
    public List<string> MissingSamples { get; set; } = [];
    public string? Error { get; set; }
}

/// <summary>
/// Admin-triggered promotion of Panel Labels edits (sample→population links + population labels)
/// from a committed snapshot onto the current environment. Export reads live state; import applies
/// the committed <c>Data/SeedData/</c> artifacts. Links go through the shared full-mirror in
/// <see cref="PanelPromotionSnapshots"/>; labels are diffed against the live <c>.ind</c> and only
/// changed rows are written.
/// </summary>
public class PanelPromotionService(ApplicationDbContext dbContext, IMergePipelineService mergeService)
    : IPanelPromotionService
{
    public async Task<PanelPromotionExportContract.Response> ExportAsync(
        string panel, CancellationToken cancellationToken = default)
    {
        var normalizedPanel = panel.Trim();

        var links = await dbContext.QpadmPopulationPanelSamples
            .AsNoTracking()
            .Where(e => e.Panel == normalizedPanel)
            .OrderBy(e => e.SampleId)
            .ThenBy(e => e.Population.Name)
            .Select(e => new PanelLinkRow
            {
                Panel = e.Panel,
                SampleId = e.SampleId,
                PopulationName = e.Population.Name,
            })
            .ToListAsync(cancellationToken);

        var ind = await mergeService.GetPanelIndRowsAsync(normalizedPanel, cancellationToken);
        var labels = ind.Rows
            .Select(r => new PanelLabelRow { Id = r.Id, Label = r.Label })
            .ToList();

        return new PanelPromotionExportContract.Response
        {
            Links = new PanelLinksSnapshot { Panels = [normalizedPanel], Links = links },
            Labels = new PanelLabelsSnapshot { Panel = normalizedPanel, Rows = labels },
        };
    }

    public async Task<PanelPromotionImportContract.Response> ImportAsync(
        string identityId, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new PanelPromotionImportContract.Response();

        // ── Links: full mirror from the committed snapshot ──────────────────────────────
        var linksSnapshot = await PanelPromotionSnapshots.LoadLinksAsync(cancellationToken);
        if (linksSnapshot is not null)
        {
            var mirror = await PanelPromotionSnapshots.ApplyLinksMirrorAsync(
                dbContext, linksSnapshot, identityId, cancellationToken);
            response.LinksAdded = mirror.Added;
            response.LinksRemoved = mirror.Removed;
            response.LinksUnchanged = mirror.Unchanged;
            response.UnknownPopulations = mirror.UnknownPopulations;
        }

        // ── Labels: diff the committed snapshot against the live .ind, apply changed rows ──
        var labelsSnapshot = await PanelPromotionSnapshots.LoadLabelsAsync(cancellationToken);
        if (labelsSnapshot is { Rows.Count: > 0 })
        {
            var labelResult = await ApplyLabelsAsync(labelsSnapshot, cancellationToken);
            response.LabelsApplied = labelResult.Applied;
            response.LabelsChanged = labelResult.Changed;
            response.LabelsTotal = labelResult.Total;
            response.MissingSamples = labelResult.MissingSamples;
            response.LabelsError = labelResult.Error;
        }

        stopwatch.Stop();
        response.DurationMs = stopwatch.ElapsedMilliseconds;
        return response;
    }

    public async Task<LabelApplyResult> ApplyLabelsAsync(
        PanelLabelsSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var result = new LabelApplyResult();
        try
        {
            var ind = await mergeService.GetPanelIndRowsAsync(snapshot.Panel, cancellationToken);
            var targetById = ind.Rows.ToDictionary(r => r.Id);

            foreach (var row in snapshot.Rows)
            {
                if (!targetById.TryGetValue(row.Id, out var target))
                {
                    result.MissingSamples.Add(row.Id);
                    continue;
                }
                result.Total++;
                if (target.Label == row.Label) continue;

                await mergeService.SetPanelIndRowLabelAsync(
                    snapshot.Panel, target.Index, row.Label, cancellationToken);
                result.Changed++;
            }

            result.Applied = true;
        }
        catch (Exception ex)
        {
            // Tools-api unreachable / validation error — surface it without failing the links mirror.
            result.Applied = false;
            result.Error = ex is MergePipelineException mpe ? mpe.Detail : ex.Message;
        }

        return result;
    }
}
