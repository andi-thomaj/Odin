using Odin.Api.Endpoints.Admin;

namespace Odin.Api.Data.Seeders;

/// <summary>
/// Applies the committed sample→population <b>links</b> snapshot
/// (<c>Data/SeedData/qpadm-population-panel-samples.json</c>) on startup, so a promoted snapshot lands
/// in the target environment on deploy. Runs <i>after</i> populations are seeded (links resolve
/// population by Name). A snapshot with no authoritative panels — the committed default — is a no-op.
///
/// Panel <b>labels</b> are deliberately NOT seeded here: they go through the admin "Promote now"
/// button only, so deploy startup never depends on tools-api.
/// </summary>
internal sealed class QpadmPopulationPanelSampleSeeder(ApplicationDbContext context)
{
    private const string SeederTag = "PanelPromotionSeeder";

    public async Task SeedAsync()
    {
        var snapshot = await PanelPromotionSnapshots.LoadLinksAsync();
        if (snapshot is null) return; // file absent — nothing to do
        await PanelPromotionSnapshots.ApplyLinksMirrorAsync(context, snapshot, SeederTag);
    }
}
