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

    // ── Dry-run (preview) — computes the counts but writes nothing ──────────────────

    [Fact]
    public async Task LinksMirror_DryRun_ComputesCountsButWritesNothing()
    {
        var pops = await SeedPopulationsAsync();
        await AddLinkAsync(pops[1].Id, "HO.002"); // would be removed (not in the snapshot)

        var snapshot = new PanelLinksSnapshot
        {
            Panels = [Panel],
            Links = [new PanelLinkRow { Panel = Panel, SampleId = "HO.001", PopulationName = pops[0].Name }],
        };

        LinksMirrorResult result;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            result = await PanelPromotionSnapshots.ApplyLinksMirrorAsync(db, snapshot, "tester", dryRun: true);
        }

        Assert.Equal(1, result.Added);
        Assert.Equal(1, result.Removed);

        // Nothing written: the original link survives, the "added" one was not inserted.
        var db2 = await GetDbContextAsync();
        var remaining = await db2.QpadmPopulationPanelSamples
            .Where(e => e.Panel == Panel).Select(e => e.SampleId).OrderBy(s => s).ToListAsync();
        Assert.Equal(["HO.002"], remaining);
    }

    [Fact]
    public async Task ApplyLabels_DryRun_CountsButWritesNothing()
    {
        await SeedPopulationsAsync();
        var fake = Factory.Services.GetRequiredService<FakeMergePipelineService>();
        fake.SetRows(Panel, [("HO.001", "M", "OldA"), ("HO.002", "F", "KeepB")]);

        var snapshot = new PanelLabelsSnapshot
        {
            Panel = Panel,
            Rows =
            [
                new PanelLabelRow { Id = "HO.001", Label = "NewA" },
                new PanelLabelRow { Id = "HO.002", Label = "KeepB" },
            ],
        };

        var result = await PanelPromotionSnapshots.ApplyLabelsAsync(fake, snapshot, dryRun: true);

        Assert.True(result.Applied);
        Assert.Equal(1, result.Changed); // HO.001 WOULD change
        Assert.Equal("OldA", fake.Rows.Single(r => r.Id == "HO.001").Label); // ...but nothing was written
    }

    // ── PanelPromotionService — export + apply (the runtime button engine) ──────────

    [Fact]
    public async Task PromotionService_Export_CapturesLinksByNameAndLabels()
    {
        var pops = await SeedPopulationsAsync();
        await AddLinkAsync(pops[2].Id, "HO.010");
        var fake = Factory.Services.GetRequiredService<FakeMergePipelineService>();
        fake.SetRows(Panel, [("HO.010", "M", "LabelX"), ("HO.011", "F", "LabelY")]);

        PanelPromotionBundle bundle;
        await using (var scope = Factory.Services.CreateAsyncScope())
            bundle = await scope.ServiceProvider.GetRequiredService<IPanelPromotionService>().ExportAsync(Panel);

        Assert.Equal(Panel, bundle.Panel);
        Assert.Equal([Panel], bundle.Links.Panels);
        var link = Assert.Single(bundle.Links.Links);
        Assert.Equal("HO.010", link.SampleId);
        Assert.Equal(pops[2].Name, link.PopulationName); // denormalised to NAME, not id
        Assert.Equal(2, bundle.Labels.Rows.Count);
        Assert.Contains(bundle.Labels.Rows, r => r.Id == "HO.010" && r.Label == "LabelX");
    }

    [Fact]
    public async Task PromotionService_ApplyDryRun_PreviewsWithoutWriting()
    {
        var pops = await SeedPopulationsAsync();
        var fake = Factory.Services.GetRequiredService<FakeMergePipelineService>();
        fake.SetRows(Panel, [("HO.001", "M", "Old")]);

        var bundle = new PanelPromotionBundle
        {
            Panel = Panel,
            Links = new PanelLinksSnapshot
            {
                Panels = [Panel],
                Links = [new PanelLinkRow { Panel = Panel, SampleId = "HO.001", PopulationName = pops[0].Name }],
            },
            Labels = new PanelLabelsSnapshot
            {
                Panel = Panel,
                Rows = [new PanelLabelRow { Id = "HO.001", Label = "New" }],
            },
        };

        PanelPromotionApplyResult result;
        await using (var scope = Factory.Services.CreateAsyncScope())
            result = await scope.ServiceProvider.GetRequiredService<IPanelPromotionService>()
                .ApplyAsync(bundle, "tester", dryRun: true);

        Assert.True(result.DryRun);
        Assert.Equal(1, result.Links.Added);
        Assert.Equal(1, result.Labels.Changed);

        // Preview only — neither the DB link nor the label was written.
        var db2 = await GetDbContextAsync();
        Assert.Equal(0, await db2.QpadmPopulationPanelSamples.CountAsync(e => e.Panel == Panel));
        Assert.Equal("Old", fake.Rows.Single(r => r.Id == "HO.001").Label);
    }
}
