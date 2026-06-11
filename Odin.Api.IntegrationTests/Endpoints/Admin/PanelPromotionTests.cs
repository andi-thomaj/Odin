using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.Admin;
using Odin.Api.Endpoints.Admin.Models;
using Odin.Api.IntegrationTests.Fakers;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.Admin;

/// <summary>
/// Coverage for the panel-promotion apply logic that the startup seeder runs on deploy: the links
/// full-mirror (against the real DB) and the label diff-apply (against an in-memory tools-api fake).
/// Exercised directly with in-memory snapshots since the committed SeedData files are empty no-ops.
/// </summary>
public class PanelPromotionTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string Panel = "HO";

    private async Task<List<(int Id, string Name)>> SeedPopulationsAsync()
    {
        await using (var scope = Factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedReferenceCatalogAsync();

        var db = await GetDbContextAsync();
        return (await db.QpadmPopulations.OrderBy(p => p.Id).Select(p => new { p.Id, p.Name }).ToListAsync())
            .Select(p => (p.Id, p.Name)).ToList();
    }

    private async Task AddLinkAsync(int populationId, string sampleId, string panel = Panel)
    {
        var db = await GetDbContextAsync();
        var now = DateTime.UtcNow;
        db.QpadmPopulationPanelSamples.Add(new QpadmPopulationPanelSample
        {
            QpadmPopulationId = populationId,
            Panel = panel,
            SampleId = sampleId,
            CreatedAt = now,
            CreatedBy = "seed",
            UpdatedAt = now,
            UpdatedBy = "seed",
        });
        await db.SaveChangesAsync();
    }

    // ── Links full-mirror (shared helper, against the real DB) ─────────────────────

    [Fact]
    public async Task LinksMirror_AddsRemovesAndKeeps_AndReportsUnknownPopulations()
    {
        var pops = await SeedPopulationsAsync();
        // Existing: pop0→HO.001 (kept), pop1→HO.002 (removed — not in snapshot).
        await AddLinkAsync(pops[0].Id, "HO.001");
        await AddLinkAsync(pops[1].Id, "HO.002");

        var snapshot = new PanelLinksSnapshot
        {
            Panels = [Panel],
            Links =
            [
                new PanelLinkRow { Panel = Panel, SampleId = "HO.001", PopulationName = pops[0].Name }, // unchanged
                new PanelLinkRow { Panel = Panel, SampleId = "HO.003", PopulationName = pops[2].Name }, // added
                new PanelLinkRow { Panel = Panel, SampleId = "HO.004", PopulationName = "No Such Population" }, // unknown
            ],
        };

        LinksMirrorResult result;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            result = await PanelPromotionSnapshots.ApplyLinksMirrorAsync(db, snapshot, "tester");
        }

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Removed);
        Assert.Equal(1, result.Unchanged);
        Assert.Equal(["No Such Population"], result.UnknownPopulations);

        var db2 = await GetDbContextAsync();
        var remaining = await db2.QpadmPopulationPanelSamples
            .Where(e => e.Panel == Panel)
            .Select(e => e.SampleId)
            .OrderBy(s => s)
            .ToListAsync();
        Assert.Equal(["HO.001", "HO.003"], remaining);
    }

    [Fact]
    public async Task LinksMirror_EmptyPanels_IsNoOp()
    {
        var pops = await SeedPopulationsAsync();
        await AddLinkAsync(pops[0].Id, "HO.001");

        var snapshot = new PanelLinksSnapshot { Panels = [], Links = [] };
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var result = await PanelPromotionSnapshots.ApplyLinksMirrorAsync(db, snapshot, "tester");
            Assert.Equal(0, result.Added);
            Assert.Equal(0, result.Removed);
        }

        var db2 = await GetDbContextAsync();
        Assert.Equal(1, await db2.QpadmPopulationPanelSamples.CountAsync(e => e.Panel == Panel));
    }

    [Fact]
    public async Task LinksMirror_PanelAuthoritativeWithNoLinks_ClearsThatPanel()
    {
        var pops = await SeedPopulationsAsync();
        await AddLinkAsync(pops[0].Id, "HO.001");
        await AddLinkAsync(pops[1].Id, "HO.002");

        var snapshot = new PanelLinksSnapshot { Panels = [Panel], Links = [] };
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var result = await PanelPromotionSnapshots.ApplyLinksMirrorAsync(db, snapshot, "tester");
            Assert.Equal(2, result.Removed);
        }

        var db2 = await GetDbContextAsync();
        Assert.Equal(0, await db2.QpadmPopulationPanelSamples.CountAsync(e => e.Panel == Panel));
    }

    [Fact]
    public async Task LinksMirror_ResolvesByPopulationName_NotId()
    {
        var pops = await SeedPopulationsAsync();
        // Snapshot references a population by NAME only — mirror must resolve it to the right id.
        var target = pops[5];
        var snapshot = new PanelLinksSnapshot
        {
            Panels = [Panel],
            Links = [new PanelLinkRow { Panel = Panel, SampleId = "HO.050", PopulationName = target.Name }],
        };

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var result = await PanelPromotionSnapshots.ApplyLinksMirrorAsync(db, snapshot, "tester");
            Assert.Equal(1, result.Added);
        }

        var db2 = await GetDbContextAsync();
        var link = await db2.QpadmPopulationPanelSamples.SingleAsync(e => e.SampleId == "HO.050");
        Assert.Equal(target.Id, link.QpadmPopulationId);
    }

    // ── Labels diff-apply (service, against the fake tools-api) ─────────────────────

    [Fact]
    public async Task ApplyLabels_WritesOnlyChangedRows_AndReportsMissing()
    {
        await SeedPopulationsAsync();
        var fake = Factory.Services.GetRequiredService<FakeMergePipelineService>();
        fake.SetRows(Panel,
        [
            ("HO.001", "M", "OldA"),
            ("HO.002", "F", "KeepB"),
            ("HO.003", "M", "OldC"),
        ]);

        var snapshot = new PanelLabelsSnapshot
        {
            Panel = Panel,
            Rows =
            [
                new PanelLabelRow { Id = "HO.001", Label = "NewA" },  // changed
                new PanelLabelRow { Id = "HO.002", Label = "KeepB" }, // unchanged
                new PanelLabelRow { Id = "HO.003", Label = "NewC" },  // changed
                new PanelLabelRow { Id = "HO.999", Label = "Ghost" }, // missing on target
            ],
        };

        var result = await PanelPromotionSnapshots.ApplyLabelsAsync(fake, snapshot);

        Assert.True(result.Applied);
        Assert.Equal(2, result.Changed);
        Assert.Equal(3, result.Total);
        Assert.Equal(["HO.999"], result.MissingSamples);

        var rowsById = fake.Rows.ToDictionary(r => r.Id, r => r.Label);
        Assert.Equal("NewA", rowsById["HO.001"]);
        Assert.Equal("KeepB", rowsById["HO.002"]);
        Assert.Equal("NewC", rowsById["HO.003"]);
    }

    [Fact]
    public async Task ApplyLabels_NoDifferences_WritesNothing()
    {
        await SeedPopulationsAsync();
        var fake = Factory.Services.GetRequiredService<FakeMergePipelineService>();
        fake.SetRows(Panel, [("HO.001", "M", "A"), ("HO.002", "F", "B")]);

        var snapshot = new PanelLabelsSnapshot
        {
            Panel = Panel,
            Rows = [new PanelLabelRow { Id = "HO.001", Label = "A" }, new PanelLabelRow { Id = "HO.002", Label = "B" }],
        };

        var result = await PanelPromotionSnapshots.ApplyLabelsAsync(fake, snapshot);

        Assert.True(result.Applied);
        Assert.Equal(0, result.Changed);
        Assert.Equal(2, result.Total);
    }
}
