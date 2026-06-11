using System.Net;
using System.Net.Http.Json;
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
/// Coverage for promoting Panel Labels edits between environments: the links full-mirror, the label
/// diff-apply, the export endpoint, and the AdminOnly boundary. The committed SeedData snapshot files
/// are intentionally empty (no-op), so link/label apply logic is exercised directly with in-memory
/// snapshots; the import endpoint itself gets a smoke test.
/// </summary>
public class PanelPromotionEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
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

        LabelApplyResult result;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IPanelPromotionService>();
            result = await service.ApplyLabelsAsync(snapshot);
        }

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

        await using var scope = Factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IPanelPromotionService>();
        var result = await service.ApplyLabelsAsync(snapshot);

        Assert.True(result.Applied);
        Assert.Equal(0, result.Changed);
        Assert.Equal(2, result.Total);
    }

    // ── Import endpoint (HTTP smoke — committed snapshot is the empty no-op default) ──

    [Fact]
    public async Task Import_WithDefaultEmptySnapshot_ReturnsZeroes()
    {
        await SeedPopulationsAsync();

        var response = await Client.PostAsync("/api/admin/panel-promotion/import", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PanelPromotionImportContract.Response>();
        Assert.NotNull(body);
        Assert.Equal(0, body.LinksAdded);
        Assert.Equal(0, body.LinksRemoved);
    }

    // ── AuthZ ──────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Import_AsScientist_IsForbidden()
    {
        var client = await CreateClientAsAsync("auth0|panel-promo-scientist2", AppRole.Scientist);
        var response = await client.PostAsync("/api/admin/panel-promotion/import", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
