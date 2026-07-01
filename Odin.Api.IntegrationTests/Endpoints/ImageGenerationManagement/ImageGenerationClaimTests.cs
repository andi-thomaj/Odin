using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.ImageGenerationManagement;
using Odin.Api.IntegrationTests.Infrastructure;
using Xunit;

namespace Odin.Api.IntegrationTests.Endpoints.ImageGenerationManagement;

/// <summary>
/// Regression guard for the deploy-interruption bug class on admin image generation. The CLAIM in
/// <see cref="IImageGenerationService.ProcessJobAsync"/> must REFUSE a fresh <c>Running</c> job (no double-run from a
/// duplicate enqueue / the reconcile racing the live worker) but RE-CLAIM a STALE <c>Running</c> job (worker died / a
/// deploy killed it mid-run) so it self-heals. Runs on the real Postgres provider — the claim uses ExecuteUpdate,
/// unsupported by EF InMemory. The factory swaps the OpenAI + R2 clients for fakes, so the reclaimed run completes.
/// </summary>
public class ImageGenerationClaimTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task ProcessJob_FreshRunningJob_IsNotReclaimed_NoDoubleRun()
    {
        var jobId = await SeedRunningJobAsync(DateTime.UtcNow);

        await ProcessAsync(jobId);

        var job = await LoadAsync(jobId);
        Assert.Equal(ImageGenerationStatus.Running, job.Status); // claim refused the fresh job
        Assert.Empty(job.Images);
    }

    [Fact]
    public async Task ProcessJob_StaleRunningJob_IsReclaimed_SoACrashedRunSelfHeals()
    {
        var jobId = await SeedRunningJobAsync(DateTime.UtcNow.AddMinutes(-30));

        await ProcessAsync(jobId);

        var job = await LoadAsync(jobId);
        // Re-claimed and executed against the fake OpenAI/R2 clients → completes (the old behaviour left it stuck Running).
        Assert.Equal(ImageGenerationStatus.Succeeded, job.Status);
        Assert.NotEmpty(job.Images);
    }

    private async Task<Guid> SeedRunningJobAsync(DateTime updatedAt)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var id = Guid.NewGuid();
        db.ImageGenerationJobs.Add(new ImageGenerationJob
        {
            Id = id,
            Mode = ImageGenerationMode.Generation,
            Status = ImageGenerationStatus.Running,
            Prompt = "a calm landscape",
            Model = "gpt-image-2",
            Size = "1024x1024",
            Quality = "low",
            Background = "auto",
            OutputFormat = "png",
            Moderation = "auto",
            N = 1,
            CreatedBy = "auth0|integration-default",
            CreatedAt = updatedAt,
            UpdatedAt = updatedAt,
        });
        await db.SaveChangesAsync();
        return id;
    }

    private async Task ProcessAsync(Guid jobId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<IImageGenerationService>();
        await svc.ProcessJobAsync(jobId);
    }

    private async Task<ImageGenerationJob> LoadAsync(Guid jobId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.ImageGenerationJobs.AsNoTracking().Include(j => j.Images).SingleAsync(j => j.Id == jobId);
    }
}
