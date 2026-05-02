using Odin.Api.Data.Seeders;

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
public class DatabaseSeeder(ApplicationDbContext context)
{
    /// <summary>Runs every seeder in dependency order — the entry point used by app startup.</summary>
    public async Task SeedAsync()
    {
        await SeedReferenceCatalogAsync();
        await new MediaFileSeeder(context).SeedAsync();
    }

    /// <summary>
    /// Ethnicities, eras + populations, and G25 reference data — safe to re-run when tables
    /// are empty (e.g. integration tests after a Respawn). Excludes the heavy media-file
    /// seeders, which integration tests don't need.
    /// </summary>
    public async Task SeedReferenceCatalogAsync()
    {
        await new EthnicityAndRegionSeeder(context).SeedAsync();
        await new QpadmEraAndPopulationSeeder(context).SeedAsync();
        await new G25Seeder(context).SeedAsync();
    }
}
