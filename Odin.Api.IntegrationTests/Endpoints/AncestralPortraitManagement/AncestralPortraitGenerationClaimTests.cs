using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.AncestralPortraitManagement;
using Odin.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace Odin.Api.IntegrationTests.Endpoints.AncestralPortraitManagement;

/// <summary>
/// Regression guard for the captured "stuck Running, 0 images after a redeploy" bug. The generation CLAIM must:
/// (a) REFUSE a fresh Running set so a duplicate enqueue / concurrent worker can't double-run it, and (b) RE-CLAIM a
/// stale Running set (the worker died / a deploy killed it mid-run) so a crashed generation self-heals instead of being
/// stranded "Painting…" forever. Runs on the real Postgres provider — the claim + heartbeat use ExecuteUpdate, which
/// EF InMemory does not support.
/// </summary>
public class AncestralPortraitGenerationClaimTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string DefaultIdentity = "auth0|integration-default";

    [Fact]
    public async Task RunGeneration_FreshRunningSet_IsNotReclaimed_NoDoubleRun()
    {
        // A set claimed by a live worker moments ago: a second worker must no-op and leave it exactly as-is.
        var setId = await SeedRunningSetAsync(DateTime.UtcNow);

        await RunGenerationAsync(setId);

        var set = await LoadSetAsync(setId);
        Assert.Equal(AncestralPortraitStatus.Running, set.Status);
        Assert.Empty(set.Portraits);
    }

    [Fact]
    public async Task RunGeneration_StaleRunningSet_IsReclaimed_SoACrashedRunSelfHeals()
    {
        // A set stuck Running with no heartbeat for 20 min = a dead worker / redeploy. The re-run must re-claim it.
        var setId = await SeedRunningSetAsync(DateTime.UtcNow.AddMinutes(-20));

        await RunGenerationAsync(setId);

        var set = await LoadSetAsync(setId);
        // Re-claimed and executed: with no face photos seeded it terminates as Failed — the key point is that it LEFT
        // the orphaned Running state (the old behaviour left it stuck Running forever).
        Assert.Equal(AncestralPortraitStatus.Failed, set.Status);
        Assert.False(string.IsNullOrWhiteSpace(set.Error));
    }

    private async Task<Guid> SeedRunningSetAsync(DateTime updatedAt)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(u => u.IdentityId == DefaultIdentity);
        var id = Guid.NewGuid();
        // OrderId has no FK (only an index) → no order needed; the face-photo check fails before the order is loaded.
        db.AncestralPortraitSets.Add(new AncestralPortraitSet
        {
            Id = id,
            OrderId = 999_000,
            UserId = user.Id,
            TransactionId = id.ToString("N"),
            Status = AncestralPortraitStatus.Running,
            CreatedBy = DefaultIdentity,
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task RunGenerationAsync(Guid setId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IAncestralPortraitService>();
        await svc.RunGenerationAsync(setId);
    }

    private async Task<AncestralPortraitSet> LoadSetAsync(Guid setId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.AncestralPortraitSets.AsNoTracking().Include(s => s.Portraits).SingleAsync(s => s.Id == setId);
    }
}
