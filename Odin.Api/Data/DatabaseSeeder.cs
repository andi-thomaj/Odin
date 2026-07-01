using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Odin.Api.Data.Seeders;
using Odin.Api.Endpoints.Admin;
using Odin.Api.Endpoints.MergeManagement;

namespace Odin.Api.Data;

/// <summary>
/// Coordinator for first-run database seeding. Each concrete seeding step lives
/// in its own class under <c>Data/Seeders/</c>; this class is just the orchestrator
/// the API calls on startup (<see cref="ApplicationDbContextInitializer"/>) and
/// integration tests call after database resets.
///
/// All sub-seeders are idempotent — they no-op when their target table already
/// has rows — so re-running <see cref="SeedAsync"/> is safe.
/// </summary>
public class DatabaseSeeder(
    ApplicationDbContext context,
    IMergePipelineService mergeService,
    ILogger<DatabaseSeeder> logger)
{
    /// <summary>Runs every seeder in dependency order — the entry point used by app startup.</summary>
    public async Task SeedAsync()
    {
        await SeedReferenceCatalogAsync();
        await ApplyPanelLabelsAsync();
        await new MediaFileSeeder(context).SeedAsync();
    }

    /// <summary>
    /// Applies the committed panel-labels snapshot (<c>Data/SeedData/panel-labels-HO.json</c>) to the
    /// live <c>.ind</c> via tools-api — the deploy-time half of panel promotion (links are mirrored in
    /// <see cref="SeedReferenceCatalogAsync"/>). Runs OUTSIDE the catalog transaction (it's an HTTP call,
    /// not a DB op) and is non-fatal: the committed default is empty (no-op, no tools-api call), and a
    /// tools-api failure is logged, not thrown, so a deploy never breaks on it.
    /// </summary>
    private async Task ApplyPanelLabelsAsync()
    {
        var snapshot = await PanelPromotionSnapshots.LoadLabelsAsync();
        if (snapshot is not { Rows.Count: > 0 }) return; // no committed labels → nothing to apply

        var result = await PanelPromotionSnapshots.ApplyLabelsAsync(mergeService, snapshot);
        if (result.Applied)
            logger.LogInformation(
                "Panel-labels promotion: {Changed}/{Total} label(s) changed, {Missing} sample(s) not found.",
                result.Changed, result.Total, result.MissingSamples.Count);
        else
            logger.LogWarning("Panel-labels promotion skipped (tools-api): {Error}", result.Error);
    }

    /// <summary>
    /// Ethnicities, eras + populations, and G25 reference data — safe to re-run when tables
    /// are empty (e.g. integration tests after a Respawn). Excludes the heavy media-file
    /// seeders, which integration tests don't need.
    ///
    /// Wrapped in a single transaction so a failure halfway through doesn't leave a
    /// half-populated reference catalog: the per-seeder <c>AnyAsync</c> idempotency check
    /// would otherwise treat the partial state as "already seeded" on the next startup.
    /// </summary>
    public async Task SeedReferenceCatalogAsync()
    {
        await using var transaction = await context.Database.BeginTransactionAsync();
        await new EthnicityAndRegionSeeder(context).SeedAsync();
        await new QpadmEraAndPopulationSeeder(context).SeedAsync();
        await new G25Seeder(context).SeedAsync();
        // Links depend on populations being present; applies the committed promotion snapshot (no-op by default).
        await new QpadmPopulationPanelSampleSeeder(context).SeedAsync();
        await transaction.CommitAsync();
    }
}
