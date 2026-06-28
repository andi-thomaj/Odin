using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.ImageGenerationManagement;
using Odin.Api.Endpoints.ImageGenerationManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;
using Odin.Api.Pagination;

namespace Odin.Api.IntegrationTests.Endpoints.ImageGenerationManagement;

public class ImageGenerationEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string Base = "/api/admin/images";

    private static readonly byte[] OnePixelPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M8AAAMBAQDJ/pLvAAAAAElFTkSuQmCC");

    private static MultipartFormDataContent PngForm(byte[] bytes, string fileName, string mime)
    {
        var form = new MultipartFormDataContent();
        var part = new ByteArrayContent(bytes);
        part.Headers.ContentType = new MediaTypeHeaderValue(mime);
        form.Add(part, "file", fileName);
        return form;
    }

    [Fact]
    public async Task Generate_Sync_ReturnsCompletedJobWithImage()
    {
        var response = await Client.PostAsJsonAsync($"{Base}/generate", new { prompt = "a friendly cat", n = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var job = await response.Content.ReadFromJsonAsync<ImageJobContract.Response>();
        Assert.NotNull(job);
        Assert.Equal("Succeeded", job!.Status);
        Assert.Equal("Generation", job.Mode);
        Assert.Single(job.Images);
        Assert.False(string.IsNullOrWhiteSpace(job.Images[0].Url));
        Assert.Equal(30, job.UsageTotalTokens);
    }

    [Fact]
    public async Task Generate_Async_EnqueuesThenWorkerCompletes()
    {
        var response = await Client.PostAsJsonAsync($"{Base}/generate", new { prompt = "async cat", n = 1, @async = true });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var pending = await response.Content.ReadFromJsonAsync<ImageJobContract.Response>();
        Assert.NotNull(pending);
        Assert.Equal("Pending", pending!.Status);
        Assert.True(pending.IsAsync);

        // No Hangfire server runs under the Testing environment — drive the worker directly.
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var worker = scope.ServiceProvider.GetRequiredService<IImageGenerationWorker>();
            await worker.RunAsync(pending.JobId);
        }

        var job = await Client.GetFromJsonAsync<ImageJobContract.Response>($"{Base}/jobs/{pending.JobId}");
        Assert.NotNull(job);
        Assert.Equal("Succeeded", job!.Status);
        Assert.Single(job.Images);
    }

    [Fact]
    public async Task Generate_ModerationBlocked_Returns422()
    {
        var response = await Client.PostAsJsonAsync($"{Base}/generate", new { prompt = "moderation-boom please", n = 1 });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);

        // The job is still recorded as Failed in history.
        var list = await Client.GetFromJsonAsync<PageResponse<ImageJobContract.Response>>($"{Base}/jobs");
        Assert.NotNull(list);
        Assert.Contains(list!.Items, j => j.Status == "Failed" && j.ErrorCode == "moderation_blocked");
    }

    [Fact]
    public async Task Generate_RateLimited_Returns429()
    {
        var response = await Client.PostAsJsonAsync($"{Base}/generate", new { prompt = "rate-boom now", n = 1 });
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Generate_CustomDimensions_AreSelectable()
    {
        // gpt-image-2 accepts arbitrary dimensions (multiples of 16, ≤3840, ≤3:1, in the pixel range).
        var response = await Client.PostAsJsonAsync($"{Base}/generate", new
        {
            prompt = "a wide landscape",
            size = "2048x1152",
            quality = "high",
            n = 1,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var job = await response.Content.ReadFromJsonAsync<ImageJobContract.Response>();
        Assert.Equal("2048x1152", job!.Size);
        Assert.Equal("high", job.Quality);
        Assert.Equal("Succeeded", job.Status);
    }

    [Fact]
    public async Task Generate_InvalidSize_ReturnsBadRequest()
    {
        // 999 is not a multiple of 16 — rejected by the gpt-image-2 dimension constraints.
        var response = await Client.PostAsJsonAsync($"{Base}/generate", new { prompt = "x", size = "999x999" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ReferenceUpload_ThenGenerateFromReferences_Succeeds()
    {
        using var form = PngForm(OnePixelPng, "ref.png", "image/png");
        var uploadResponse = await Client.PostAsync($"{Base}/reference-images", form);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);
        var reference = await uploadResponse.Content.ReadFromJsonAsync<ReferenceImageContract.Response>();
        Assert.NotNull(reference);
        Assert.False(string.IsNullOrWhiteSpace(reference!.Url));

        var editResponse = await Client.PostAsJsonAsync($"{Base}/generate-from-references", new
        {
            prompt = "make it a painting",
            referenceImageIds = new[] { reference.Id },
        });

        Assert.Equal(HttpStatusCode.OK, editResponse.StatusCode);
        var job = await editResponse.Content.ReadFromJsonAsync<ImageJobContract.Response>();
        Assert.NotNull(job);
        Assert.Equal("Edit", job!.Mode);
        Assert.Equal("Succeeded", job.Status);
        Assert.Single(job.Images);
        Assert.Equal(new[] { reference.Id }, job.ReferenceImageIds);
    }

    [Fact]
    public async Task ReferenceUpload_NonImageBytes_ReturnsBadRequest()
    {
        using var form = PngForm("this is definitely not an image"u8.ToArray(), "fake.png", "image/png");
        var response = await Client.PostAsync($"{Base}/reference-images", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GenerateFromReferences_MissingReference_ReturnsBadRequest()
    {
        var response = await Client.PostAsJsonAsync($"{Base}/generate-from-references", new
        {
            prompt = "edit",
            referenceImageIds = new[] { 999_999 },
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Settings_GetReturnsDefaults_PutUpdates()
    {
        var defaults = await Client.GetFromJsonAsync<ImageGenerationSettingsContract.Response>($"{Base}/settings");
        Assert.NotNull(defaults);
        Assert.Equal("gpt-image-2", defaults!.Model);

        var update = await Client.PutAsJsonAsync($"{Base}/settings", new
        {
            size = "1536x1024",
            quality = "high",
            background = "auto",
            outputFormat = "png",
            moderation = "auto",
            defaultN = 2,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await update.Content.ReadFromJsonAsync<ImageGenerationSettingsContract.Response>();
        Assert.Equal("1536x1024", updated!.Size);
        Assert.Equal(2, updated.DefaultN);

        // Persisted defaults now feed a request that omits size/n.
        var generated = await Client.PostAsJsonAsync($"{Base}/generate", new { prompt = "uses defaults" });
        var job = await generated.Content.ReadFromJsonAsync<ImageJobContract.Response>();
        Assert.Equal("1536x1024", job!.Size);
        Assert.Equal(2, job.N);
        Assert.Equal(2, job.Images.Count);
    }

    [Fact]
    public async Task Usage_ReturnsBucketsAndCosts()
    {
        var usage = await Client.GetFromJsonAsync<OpenAIUsageContract.Response>($"{Base}/usage");
        Assert.NotNull(usage);
        Assert.Equal(3, usage!.TotalImages);
        Assert.Equal("usd", usage.Currency);
        Assert.NotEmpty(usage.CostBuckets);
    }

    [Fact]
    public async Task Generate_AsNonAdmin_IsForbidden()
    {
        var userClient = await CreateClientAsAsync("auth0|image-plain-user", AppRole.User);
        var response = await userClient.PostAsJsonAsync($"{Base}/generate", new { prompt = "nope", n = 1 });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteJob_RemovesItFromHistory()
    {
        var created = await Client.PostAsJsonAsync($"{Base}/generate", new { prompt = "to delete", n = 1 });
        var job = await created.Content.ReadFromJsonAsync<ImageJobContract.Response>();

        var delete = await Client.DeleteAsync($"{Base}/jobs/{job!.JobId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var get = await Client.GetAsync($"{Base}/jobs/{job.JobId}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }
}
