using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.OrderManagement.Models;
using Odin.Api.IntegrationTests.Fakers;
using Odin.Api.IntegrationTests.Infrastructure;
using static Odin.Api.IntegrationTests.Fakers.TestDataHelper;

namespace Odin.Api.IntegrationTests.Endpoints.MediaManagement;

public class MediaEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // ── Helpers ────────────────────────────────────────────────────

    private async Task SeedReferenceCatalog()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedReferenceCatalogAsync();
    }

    private async Task<MusicTrack> GetFirstMusicTrackAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.MusicTracks.FirstAsync();
    }

    private async Task<Population> GetFirstPopulationWithVideoFileNameAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Populations.FirstAsync(p => p.VideoFileName != "");
    }

    private async Task SeedMusicTrackFileAsync(int musicTrackId, string fileName = "test.wav")
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.MusicTrackFiles.Add(new MusicTrackFile
        {
            MusicTrackId = musicTrackId,
            FileName = fileName,
            FileData = new byte[] { 0x52, 0x49, 0x46, 0x46 }, // RIFF header stub
            ContentType = "audio/wav",
            FileSizeBytes = 4,
            CreatedBy = "test-seed",
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedPopulationVideoFileAsync(int populationId, string fileName = "test.mp4")
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.PopulationVideoFiles.Add(new PopulationVideoFile
        {
            PopulationId = populationId,
            FileName = fileName,
            FileData = new byte[] { 0x00, 0x00, 0x00, 0x1C, 0x66, 0x74, 0x79, 0x70 }, // ftyp header stub
            ContentType = "video/mp4",
            FileSizeBytes = 8,
            CreatedBy = "test-seed",
        });
        await db.SaveChangesAsync();
    }

    // ── GET /api/media/audio/{musicTrackId} ───────────────────────

    [Fact]
    public async Task DownloadAudio_ExistingTrack_ReturnsFile()
    {
        await SeedReferenceCatalog();
        var track = await GetFirstMusicTrackAsync();
        await SeedMusicTrackFileAsync(track.Id, track.FileName);

        var response = await Client.GetAsync($"/api/media/audio/{track.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("audio/wav", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task DownloadAudio_NoFile_ReturnsNotFound()
    {
        await SeedReferenceCatalog();
        var track = await GetFirstMusicTrackAsync();

        var response = await Client.GetAsync($"/api/media/audio/{track.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DownloadAudio_NonExistentTrack_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/media/audio/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DownloadAudio_Unauthenticated_ReturnsUnauthorized()
    {
        using var unauthClient = CreateUnauthenticatedClient(Factory);
        var response = await unauthClient.GetAsync("/api/media/audio/1");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── GET /api/media/video/{populationId} ───────────────────────

    [Fact]
    public async Task DownloadVideo_ExistingPopulation_ReturnsFile()
    {
        await SeedReferenceCatalog();
        var pop = await GetFirstPopulationWithVideoFileNameAsync();
        await SeedPopulationVideoFileAsync(pop.Id, pop.VideoFileName);

        var response = await Client.GetAsync($"/api/media/video/{pop.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("video/mp4", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task DownloadVideo_NoFile_ReturnsNotFound()
    {
        await SeedReferenceCatalog();
        var pop = await GetFirstPopulationWithVideoFileNameAsync();

        var response = await Client.GetAsync($"/api/media/video/{pop.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── PUT /api/media/audio/{musicTrackId} (Admin upload) ────────

    [Fact]
    public async Task UploadAudio_Admin_Succeeds()
    {
        await SeedReferenceCatalog();
        var track = await GetFirstMusicTrackAsync();

        var wavBytes = new byte[] { 0x52, 0x49, 0x46, 0x46, 0x00, 0x00, 0x00, 0x00 };
        using var content = new MultipartFormDataContent();
        var filePart = new ByteArrayContent(wavBytes);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(filePart, "file", "culture-track.wav");

        var response = await Client.PutAsync($"/api/media/audio/{track.Id}", content);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Verify file was stored with original name
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.MusicTrackFiles.FirstOrDefaultAsync(f => f.MusicTrackId == track.Id);
        Assert.NotNull(stored);
        Assert.Equal("culture-track.wav", stored!.FileName);
        Assert.Equal(wavBytes.Length, stored.FileSizeBytes);
    }

    [Fact]
    public async Task UploadAudio_NonAdmin_ReturnsForbidden()
    {
        await SeedReferenceCatalog();
        var track = await GetFirstMusicTrackAsync();

        using var userClient = CreateClientWithRole(Factory, "auth0|user-test", "User");
        await CreateUserAsync(userClient, identityId: "auth0|user-test");

        using var content = new MultipartFormDataContent();
        var filePart = new ByteArrayContent([0x52, 0x49, 0x46, 0x46]);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        content.Add(filePart, "file", "test.wav");

        var response = await userClient.PutAsync($"/api/media/audio/{track.Id}", content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── PUT /api/media/video/{populationId} (Admin upload) ────────

    [Fact]
    public async Task UploadVideo_Admin_Succeeds()
    {
        await SeedReferenceCatalog();
        var pop = await GetFirstPopulationWithVideoFileNameAsync();

        var mp4Bytes = new byte[] { 0x00, 0x00, 0x00, 0x1C, 0x66, 0x74, 0x79, 0x70 };
        using var content = new MultipartFormDataContent();
        var filePart = new ByteArrayContent(mp4Bytes);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        content.Add(filePart, "file", "Ancient Greek.mp4");

        var response = await Client.PutAsync($"/api/media/video/{pop.Id}", content);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.PopulationVideoFiles.FirstOrDefaultAsync(f => f.PopulationId == pop.Id);
        Assert.NotNull(stored);
        Assert.Equal("Ancient Greek.mp4", stored!.FileName);
    }

    // ── qpAdm result HasAudioFile / HasVideoFile flags ────────────

    [Fact]
    public async Task GetQpadmResult_WithMediaFiles_HasAudioFileIsTrue()
    {
        await SeedReferenceCatalog();
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);
        await SetOrderStatusAsync(Factory.Services, created.Id, OrderStatus.Completed);
        await SeedQpadmResultAsync(created.GeneticInspectionId);

        // Seed audio file for the first population's music track
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var era = await db.Eras.Include(e => e.Populations).ThenInclude(p => p.MusicTrack).FirstAsync();
            var pop = era.Populations.First();
            await SeedMusicTrackFileAsync(pop.MusicTrackId, pop.MusicTrack.FileName);
        }

        var response = await Client.GetAsync($"/api/orders/{created.Id}/qpadm-result");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<GetOrderQpadmResultContract.Response>(JsonOptions);
        var allPops = result!.EraGroups.SelectMany(eg => eg.Populations).ToList();

        // At least one population should have HasAudioFile = true
        Assert.Contains(allPops, p => p.HasAudioFile);
        // All populations should have a valid MusicTrackId > 0
        Assert.All(allPops, p => Assert.True(p.MusicTrackId > 0));
    }

    [Fact]
    public async Task GetQpadmResult_WithoutMediaFiles_HasAudioFileIsFalse()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);
        await SetOrderStatusAsync(Factory.Services, created.Id, OrderStatus.Completed);
        await SeedQpadmResultAsync(created.GeneticInspectionId);

        var response = await Client.GetAsync($"/api/orders/{created.Id}/qpadm-result");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<GetOrderQpadmResultContract.Response>(JsonOptions);
        var allPops = result!.EraGroups.SelectMany(eg => eg.Populations).ToList();

        // No media files seeded → all should be false
        Assert.All(allPops, p => Assert.False(p.HasAudioFile));
        Assert.All(allPops, p => Assert.False(p.HasVideoFile));
    }

    [Fact]
    public async Task GetQpadmResult_WithVideoFile_HasVideoFileIsTrue()
    {
        await SeedReferenceCatalog();
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);
        await SetOrderStatusAsync(Factory.Services, created.Id, OrderStatus.Completed);

        // Seed qpadm result using a population that has a video filename
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var popWithVideo = await db.Populations.FirstAsync(p => p.VideoFileName != "");

            var gi = await db.GeneticInspections.FirstAsync(gi => gi.Id == created.GeneticInspectionId);
            var qr = new QpadmResult { GeneticInspectionId = gi.Id, CreatedBy = "test-seed" };
            db.QpadmResults.Add(qr);
            await db.SaveChangesAsync();

            var eg = new QpadmResultEraGroup
            {
                QpadmResultId = qr.Id,
                EraId = popWithVideo.EraId,
                PiValue = 0.05m,
                RightSources = "WHG",
                LeftSources = "Anatolia_N",
            };
            db.Set<QpadmResultEraGroup>().Add(eg);
            await db.SaveChangesAsync();

            db.Set<QpadmResultPopulation>().Add(new QpadmResultPopulation
            {
                QpadmResultEraGroupId = eg.Id,
                PopulationId = popWithVideo.Id,
                Percentage = 100m,
                StandardError = 0.5m,
                ZScore = 1.0m,
            });
            await db.SaveChangesAsync();

            await SeedPopulationVideoFileAsync(popWithVideo.Id, popWithVideo.VideoFileName);
        }

        var response = await Client.GetAsync($"/api/orders/{created.Id}/qpadm-result");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<GetOrderQpadmResultContract.Response>(JsonOptions);
        var allPops = result!.EraGroups.SelectMany(eg => eg.Populations).ToList();

        Assert.Contains(allPops, p => p.HasVideoFile);
    }

    [Fact]
    public async Task GetQpadmResult_PopulationsIncludeGeoJson()
    {
        await SeedReferenceCatalog();
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);
        await SetOrderStatusAsync(Factory.Services, created.Id, OrderStatus.Completed);
        await SeedQpadmResultAsync(created.GeneticInspectionId);

        var response = await Client.GetAsync($"/api/orders/{created.Id}/qpadm-result");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<GetOrderQpadmResultContract.Response>(JsonOptions);
        var allPops = result!.EraGroups.SelectMany(eg => eg.Populations).ToList();

        // Populations seeded by SeedReferenceCatalogAsync have GeoJson from population-geojson.json
        Assert.NotEmpty(allPops);
        // GeoJson should be present in the response (not null for populations with geodata)
        Assert.All(allPops, p => Assert.NotNull(p.GeoJson));
    }

    [Fact]
    public async Task GetQpadmResult_IntroTrackIdIsPopulated()
    {
        await SeedReferenceCatalog();
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);
        await SetOrderStatusAsync(Factory.Services, created.Id, OrderStatus.Completed);
        await SeedQpadmResultAsync(created.GeneticInspectionId);

        var response = await Client.GetAsync($"/api/orders/{created.Id}/qpadm-result");
        var result = await response.Content.ReadFromJsonAsync<GetOrderQpadmResultContract.Response>(JsonOptions);

        // Intro track (DisplayOrder 0) should be seeded by SeedReferenceCatalogAsync
        Assert.NotNull(result!.IntroTrackId);
        Assert.True(result.IntroTrackId > 0);
    }

    // ── Shared helpers ────────────────────────────────────────────

    private async Task SeedQpadmResultAsync(int geneticInspectionId)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedReferenceCatalogAsync();

        var era = await db.Eras.Include(e => e.Populations).FirstAsync();
        var populations = era.Populations.Take(2).ToList();

        var qpadmResult = new QpadmResult { GeneticInspectionId = geneticInspectionId, CreatedBy = "test-seed" };
        db.QpadmResults.Add(qpadmResult);
        await db.SaveChangesAsync();

        var eraGroup = new QpadmResultEraGroup
        {
            QpadmResultId = qpadmResult.Id,
            EraId = era.Id,
            PiValue = 0.05m,
            RightSources = "WHG, EHG",
            LeftSources = "Anatolia_N"
        };
        db.Set<QpadmResultEraGroup>().Add(eraGroup);
        await db.SaveChangesAsync();

        db.Set<QpadmResultPopulation>().Add(new QpadmResultPopulation
        {
            QpadmResultEraGroupId = eraGroup.Id,
            PopulationId = populations[0].Id,
            Percentage = 60m,
            StandardError = 1.2m,
            ZScore = 2.5m
        });
        db.Set<QpadmResultPopulation>().Add(new QpadmResultPopulation
        {
            QpadmResultEraGroupId = eraGroup.Id,
            PopulationId = populations[1].Id,
            Percentage = 40m,
            StandardError = 0.8m,
            ZScore = 1.9m
        });
        await db.SaveChangesAsync();
    }
}
