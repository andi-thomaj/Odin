using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.Admin.Models;

namespace Odin.Api.Endpoints.Admin;

/// <summary>Outcome of mirroring a committed links snapshot onto a target database.</summary>
public sealed class LinksMirrorResult
{
    public int Added { get; set; }
    public int Removed { get; set; }
    public int Unchanged { get; set; }
    public List<string> UnknownPopulations { get; set; } = [];
}

/// <summary>
/// Loads the committed panel-promotion snapshot files from <c>Data/SeedData/</c> and applies the
/// links snapshot as a full mirror. Shared by the admin <c>PanelPromotionService</c> (the "Promote
/// now" button) and the startup <c>QpadmPopulationPanelSampleSeeder</c> so both converge identically.
/// </summary>
public static class PanelPromotionSnapshots
{
    public const string LinksFileName = "qpadm-population-panel-samples.json";
    public const string LabelsFileName = "panel-labels-HO.json";

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static string LinksPath() =>
        Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", LinksFileName);

    public static string LabelsPath() =>
        Path.Combine(AppContext.BaseDirectory, "Data", "SeedData", LabelsFileName);

    /// <summary>Reads the committed links snapshot, or null when the file is absent.</summary>
    public static async Task<PanelLinksSnapshot?> LoadLinksAsync(CancellationToken cancellationToken = default)
    {
        var path = LinksPath();
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<PanelLinksSnapshot>(json, JsonOpts);
    }

    /// <summary>Reads the committed labels snapshot, or null when the file is absent.</summary>
    public static async Task<PanelLabelsSnapshot?> LoadLabelsAsync(CancellationToken cancellationToken = default)
    {
        var path = LabelsPath();
        if (!File.Exists(path)) return null;
        var json = await File.ReadAllTextAsync(path, cancellationToken);
        return JsonSerializer.Deserialize<PanelLabelsSnapshot>(json, JsonOpts);
    }

    /// <summary>
    /// Full-mirrors the links snapshot onto <paramref name="db"/> for the panels it declares as
    /// authoritative: inserts missing links, deletes links not in the snapshot, leaves matches.
    /// A snapshot with no authoritative panels is a no-op (never "delete everything"). Links whose
    /// population name doesn't resolve on the target are skipped and reported.
    /// </summary>
    public static async Task<LinksMirrorResult> ApplyLinksMirrorAsync(
        ApplicationDbContext db, PanelLinksSnapshot snapshot, string identityId,
        CancellationToken cancellationToken = default)
    {
        var result = new LinksMirrorResult();

        var panels = (snapshot.Panels ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct()
            .ToList();
        if (panels.Count == 0) return result; // nothing authoritative → no-op

        var nameMap = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var p in await db.QpadmPopulations.AsNoTracking()
                     .Select(p => new { p.Id, p.Name }).ToListAsync(cancellationToken))
            nameMap[p.Name] = p.Id; // names are unique per environment

        var desired = new HashSet<(int PopId, string Panel, string SampleId)>();
        var unknown = new HashSet<string>(StringComparer.Ordinal);
        foreach (var link in snapshot.Links ?? [])
        {
            var panel = (link.Panel ?? string.Empty).Trim();
            if (!panels.Contains(panel)) continue; // only mirror authoritative panels
            var sampleId = (link.SampleId ?? string.Empty).Trim();
            if (sampleId.Length == 0) continue;
            var name = (link.PopulationName ?? string.Empty).Trim();
            if (!nameMap.TryGetValue(name, out var popId))
            {
                if (name.Length > 0) unknown.Add(name);
                continue;
            }
            desired.Add((popId, panel, sampleId));
        }
        result.UnknownPopulations = unknown.OrderBy(n => n, StringComparer.Ordinal).ToList();

        var existing = await db.QpadmPopulationPanelSamples
            .Where(e => panels.Contains(e.Panel))
            .ToListAsync(cancellationToken);
        var existingKeys = existing
            .Select(e => (e.QpadmPopulationId, e.Panel, e.SampleId))
            .ToHashSet();

        var now = DateTime.UtcNow;
        foreach (var d in desired)
        {
            if (existingKeys.Contains((d.PopId, d.Panel, d.SampleId)))
            {
                result.Unchanged++;
                continue;
            }
            db.QpadmPopulationPanelSamples.Add(new QpadmPopulationPanelSample
            {
                QpadmPopulationId = d.PopId,
                Panel = d.Panel,
                SampleId = d.SampleId,
                CreatedAt = now,
                CreatedBy = identityId,
                UpdatedAt = now,
                UpdatedBy = identityId,
            });
            result.Added++;
        }

        foreach (var e in existing)
        {
            if (desired.Contains((e.QpadmPopulationId, e.Panel, e.SampleId))) continue;
            db.QpadmPopulationPanelSamples.Remove(e);
            result.Removed++;
        }

        await db.SaveChangesAsync(cancellationToken);
        return result;
    }
}
