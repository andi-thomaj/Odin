using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.CatalogManagement.Models;
using Odin.Api.Endpoints.OrderManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.OrderManagement;

public class OrderEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = await response.Content.ReadFromJsonAsync<List<GetOrderContract.Response>>();
        Assert.NotNull(orders);
    }

    [Fact]
    public async Task Create_WithExpeditedAddon_ChargesBasePlusAddon()
    {
        var catalogResponse = await Client.GetAsync("/api/catalog/products");
        catalogResponse.EnsureSuccessStatusCode();
        var products = await catalogResponse.Content.ReadFromJsonAsync<List<GetCatalogProductContract.ProductResponse>>(JsonOptions);
        Assert.NotNull(products);
        var expeditedId = products!.SelectMany(p => p.Addons).Single(a => a.Code == "EXPEDITED").Id;

        int regionId;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ethnicity = new Ethnicity { Name = "OrderTestEth" };
            db.Ethnicities.Add(ethnicity);
            await db.SaveChangesAsync();
            var region = new Region { Name = "OrderTestRegion", Ethnicity = ethnicity };
            db.Regions.Add(region);
            await db.SaveChangesAsync();
            regionId = region.Id;
        }

        var fileBytes = "rsid,chromosome,position,genotype\nrs1,1,1,AA\n"u8.ToArray();
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Jane"), "FirstName");
        content.Add(new StringContent("Doe"), "LastName");
        content.Add(new StringContent("Female"), "Gender");
        content.Add(new StringContent("0"), "Service");
        content.Add(new StringContent(regionId.ToString()), "RegionIds");
        content.Add(new StringContent(expeditedId.ToString()), "AddonIds");
        var filePart = new ByteArrayContent(fileBytes);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(filePart, "File", "kit.csv");

        var response = await Client.PostAsync("/api/orders", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(69.99m, created!.Price);
        Assert.Equal("qpAdm", created.Service);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lines = await db.OrderLineAddons.Where(l => l.OrderId == created.Id).ToListAsync();
            Assert.Single(lines);
            Assert.Equal(expeditedId, lines[0].ProductAddonId);
            Assert.Equal(20m, lines[0].UnitPriceSnapshot);
        }
    }

    [Fact]
    public async Task Create_WithoutAddonIdsInForm_UsesBasePriceOnly()
    {
        int regionId;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ethnicity = new Ethnicity { Name = "OrderTestEthNoAddon" };
            db.Ethnicities.Add(ethnicity);
            await db.SaveChangesAsync();
            var region = new Region { Name = "OrderTestRegionNoAddon", Ethnicity = ethnicity };
            db.Regions.Add(region);
            await db.SaveChangesAsync();
            regionId = region.Id;
        }

        var fileBytes = "rsid,chromosome,position,genotype\nrs1,1,1,AA\n"u8.ToArray();
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Alex"), "FirstName");
        content.Add(new StringContent("No"), "LastName");
        content.Add(new StringContent("Male"), "Gender");
        content.Add(new StringContent("0"), "Service");
        content.Add(new StringContent(regionId.ToString()), "RegionIds");
        // Intentionally omit AddonIds — form binder leaves list null; pricing must treat as empty.
        var filePart = new ByteArrayContent(fileBytes);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(filePart, "File", "kit.csv");

        var response = await Client.PostAsync("/api/orders", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(49.99m, created!.Price);
        Assert.Equal("qpAdm", created.Service);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var lines = await db.OrderLineAddons.Where(l => l.OrderId == created.Id).ToListAsync();
            Assert.Empty(lines);
        }
    }
}
