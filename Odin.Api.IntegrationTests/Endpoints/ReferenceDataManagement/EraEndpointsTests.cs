using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Endpoints.UserManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.ReferenceDataManagement;

public class EraEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private async Task SeedReferenceDataAsync()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedReferenceCatalogAsync();
    }

    // ── Seeding: Eras ──────────────────────────────────────────────

    [Fact]
    public async Task Seed_CreatesTwoEras()
    {
        await SeedReferenceDataAsync();

        var db = await GetDbContextAsync();
        var eras = await db.QpadmEras.OrderBy(e => e.Id).ToListAsync();

        Assert.Equal(2, eras.Count);
        Assert.Equal("Hunter Gatherer and Neolithic Farmer", eras[0].Name);
        Assert.Equal("Classical Antiquity", eras[1].Name);
    }

    // ── Seeding: Populations ───────────────────────────────────────

    [Fact]
    public async Task Seed_Creates30Populations()
    {
        await SeedReferenceDataAsync();

        var db = await GetDbContextAsync();
        var count = await db.QpadmPopulations.CountAsync();

        Assert.Equal(30, count);
    }

    [Fact]
    public async Task Seed_Era1Contains11Populations()
    {
        await SeedReferenceDataAsync();

        var db = await GetDbContextAsync();
        var era = await db.QpadmEras.Include(e => e.Populations)
            .SingleAsync(e => e.Name == "Hunter Gatherer and Neolithic Farmer");

        Assert.Equal(11, era.Populations.Count);

        var expectedNames = new[]
        {
            "Anatolian Neolithic Farmer", "Western Steppe Herder", "Western Hunter Gatherer",
            "Caucasian Hunter Gatherer", "Iranian Neolithic Farmer", "Natufian",
            "North African Farmer", "Northeast Asian", "Native American",
            "Ancestral South Indian", "Sub Saharan Africans",
        };

        var actualNames = era.Populations.Select(p => p.Name).OrderBy(n => n).ToList();
        Assert.Equal(expectedNames.OrderBy(n => n), actualNames);
    }

    [Fact]
    public async Task Seed_Era2Contains19Populations()
    {
        await SeedReferenceDataAsync();

        var db = await GetDbContextAsync();
        var era = await db.QpadmEras.Include(e => e.Populations)
            .SingleAsync(e => e.Name == "Classical Antiquity");

        Assert.Equal(19, era.Populations.Count);

        var expectedNames = new[]
        {
            "Illyrian", "Ancient Greek", "Thracian", "Hittite & Phrygian",
            "Phoenician", "Celtic", "Iberian", "Punic Carthage",
            "Hellenistic Pontus", "Roman West Anatolia", "Latin and Etruscan",
            "Roman Moesia Superior", "Roman East Mediterranean", "Germanic",
            "Medieval Slavic", "Roman North Africa",
            "Baltic", "Finno-Ugric", "Saami",
        };

        var actualNames = era.Populations.Select(p => p.Name).OrderBy(n => n).ToList();
        Assert.Equal(expectedNames.OrderBy(n => n), actualNames);
    }

    // ── Seeding: GeoJSON ───────────────────────────────────────────

    [Fact]
    public async Task Seed_AllPopulationsHaveGeoJson()
    {
        await SeedReferenceDataAsync();

        var db = await GetDbContextAsync();
        var populations = await db.QpadmPopulations.ToListAsync();

        Assert.All(populations, p =>
        {
            Assert.NotNull(p.GeoJson);
            Assert.NotEmpty(p.GeoJson);
        });
    }

    [Fact]
    public async Task Seed_GeoJsonIsPolygonOrMultiPolygon()
    {
        await SeedReferenceDataAsync();

        var db = await GetDbContextAsync();
        var populations = await db.QpadmPopulations.ToListAsync();

        Assert.All(populations, p =>
        {
            Assert.NotNull(p.GeoJson);
            var doc = JsonDocument.Parse(p.GeoJson!);
            var root = doc.RootElement;

            var geoType = root.GetProperty("type").GetString();
            Assert.True(
                geoType == "Polygon" || geoType == "MultiPolygon",
                $"Population '{p.Name}' has GeoJSON type '{geoType}' — expected Polygon or MultiPolygon");

            Assert.True(root.TryGetProperty("coordinates", out _),
                $"Population '{p.Name}' GeoJSON missing 'coordinates'");
        });
    }

    [Fact]
    public async Task Seed_GeoJsonIsNotFeatureCollection()
    {
        await SeedReferenceDataAsync();

        var db = await GetDbContextAsync();
        var populations = await db.QpadmPopulations.ToListAsync();

        Assert.All(populations, p =>
        {
            Assert.DoesNotContain("FeatureCollection", p.GeoJson!);
            Assert.DoesNotContain("\"features\"", p.GeoJson!);
        });
    }

    [Fact]
    public async Task Seed_GeoJsonCoordinatesAreNonEmpty()
    {
        await SeedReferenceDataAsync();

        var db = await GetDbContextAsync();
        var populations = await db.QpadmPopulations.ToListAsync();

        Assert.All(populations, p =>
        {
            var doc = JsonDocument.Parse(p.GeoJson!);
            var coords = doc.RootElement.GetProperty("coordinates");
            Assert.True(coords.GetArrayLength() > 0,
                $"Population '{p.Name}' has empty coordinates array");
        });
    }

    [Fact]
    public async Task Seed_NativeAmericanHasMultiPolygon()
    {
        await SeedReferenceDataAsync();

        var db = await GetDbContextAsync();
        var nativeAmerican = await db.QpadmPopulations.SingleAsync(p => p.Name == "Native American");

        Assert.NotNull(nativeAmerican.GeoJson);
        var doc = JsonDocument.Parse(nativeAmerican.GeoJson!);
        var geoType = doc.RootElement.GetProperty("type").GetString();
        Assert.Equal("MultiPolygon", geoType);

        var coords = doc.RootElement.GetProperty("coordinates");
        Assert.True(coords.GetArrayLength() >= 2,
            "Native American should have at least 2 polygon rings (North + South America)");
    }

    // ── Seeding: Music Tracks ──────────────────────────────────────

    [Fact]
    public async Task Seed_Creates18MusicTracks()
    {
        await SeedReferenceDataAsync();

        var db = await GetDbContextAsync();
        var count = await db.MusicTracks.CountAsync();

        Assert.Equal(18, count);
    }

    [Fact]
    public async Task Seed_15MusicTracksLinkedToPopulations()
    {
        await SeedReferenceDataAsync();

        var db = await GetDbContextAsync();
        var tracks = await db.MusicTracks.Include(t => t.Populations).ToListAsync();
        var linked = tracks.Where(t => t.Populations.Count > 0).ToList();

        Assert.Equal(15, linked.Count);
    }

    [Fact]
    public async Task Seed_AllPopulationsHaveMusicTrack()
    {
        await SeedReferenceDataAsync();

        var db = await GetDbContextAsync();
        var withoutTrack = await db.QpadmPopulations.CountAsync(p => p.MusicTrackId == 0);

        Assert.Equal(0, withoutTrack);
    }

    // ── Seeding: Idempotency ───────────────────────────────────────

    [Fact]
    public async Task Seed_RunningTwice_DoesNotDuplicateData()
    {
        await SeedReferenceDataAsync();
        await SeedReferenceDataAsync();

        var db = await GetDbContextAsync();

        Assert.Equal(2, await db.QpadmEras.CountAsync());
        Assert.Equal(30, await db.QpadmPopulations.CountAsync());
        Assert.Equal(18, await db.MusicTracks.CountAsync());
    }

    // ── API: GET /api/eras ─────────────────────────────────────────

    [Fact]
    public async Task GetEras_ReturnsOkWithSeededData()
    {
        await SeedReferenceDataAsync();

        var response = await Client.GetAsync("/api/eras");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var eras = await response.Content.ReadFromJsonAsync<List<GetErasContract.Response>>();
        Assert.NotNull(eras);
        Assert.Equal(2, eras.Count);
    }

    [Fact]
    public async Task GetEras_Returns30PopulationsTotal()
    {
        await SeedReferenceDataAsync();

        var response = await Client.GetAsync("/api/eras");
        var eras = await response.Content.ReadFromJsonAsync<List<GetErasContract.Response>>();

        Assert.NotNull(eras);
        Assert.Equal(30, eras.Sum(e => e.Populations.Count));
    }

    [Fact]
    public async Task GetEras_PopulationsIncludeMusicTracks()
    {
        await SeedReferenceDataAsync();

        var response = await Client.GetAsync("/api/eras");
        var eras = await response.Content.ReadFromJsonAsync<List<GetErasContract.Response>>();

        Assert.NotNull(eras);
        var allPopulations = eras.SelectMany(e => e.Populations).ToList();
        Assert.All(allPopulations, p => Assert.NotNull(p.MusicTrack));
    }

    [Fact]
    public async Task GetEras_MusicTrackDisplayOrdersAreSequential()
    {
        await SeedReferenceDataAsync();

        var response = await Client.GetAsync("/api/eras");
        var eras = await response.Content.ReadFromJsonAsync<List<GetErasContract.Response>>();

        Assert.NotNull(eras);
        var displayOrders = eras.SelectMany(e => e.Populations)
            .Select(p => p.MusicTrack!.DisplayOrder)
            .Distinct()
            .OrderBy(o => o)
            .ToList();

        Assert.Equal(15, displayOrders.Count);
        Assert.Equal(1, displayOrders.First());
        Assert.Equal(15, displayOrders.Last());
    }

    [Fact]
    public async Task GetEras_PopulationsIncludeIconFileNames()
    {
        await SeedReferenceDataAsync();

        var response = await Client.GetAsync("/api/eras");
        var eras = await response.Content.ReadFromJsonAsync<List<GetErasContract.Response>>();

        Assert.NotNull(eras);
        var withIcon = eras.SelectMany(e => e.Populations).Where(p => !string.IsNullOrEmpty(p.IconFileName)).ToList();
        Assert.True(withIcon.Count > 0, "At least one population should have an IconFileName");
        Assert.All(withIcon, p => Assert.EndsWith(".svg", p.IconFileName!));
    }

    [Fact]
    public async Task Seed_AllPopulationsHaveColor()
    {
        await SeedReferenceDataAsync();

        var db = await GetDbContextAsync();
        var populations = await db.QpadmPopulations.ToListAsync();

        Assert.All(populations, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Color),
                $"Population '{p.Name}' has empty Color");
            Assert.Matches("^#[0-9A-Fa-f]{6}$", p.Color);
        });
    }

    [Fact]
    public async Task GetEras_PopulationsIncludeColor()
    {
        await SeedReferenceDataAsync();

        var response = await Client.GetAsync("/api/eras");
        var eras = await response.Content.ReadFromJsonAsync<List<GetErasContract.Response>>();

        Assert.NotNull(eras);
        var allPopulations = eras.SelectMany(e => e.Populations).ToList();
        Assert.All(allPopulations, p =>
        {
            Assert.False(string.IsNullOrWhiteSpace(p.Color),
                $"Population '{p.Name}' has empty Color in API response");
            Assert.Matches("^#[0-9A-Fa-f]{6}$", p.Color);
        });
    }

}
