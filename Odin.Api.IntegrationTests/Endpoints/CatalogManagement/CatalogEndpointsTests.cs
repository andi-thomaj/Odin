using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Odin.Api.Endpoints.CatalogManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.CatalogManagement;

public class CatalogEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task GetProducts_ReturnsQpadmWithThreeAddons()
    {
        var response = await Client.GetAsync("/api/catalog/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await response.Content.ReadFromJsonAsync<List<GetCatalogProductContract.ProductResponse>>(JsonOptions);
        Assert.NotNull(products);
        var qpadm = Assert.Single(products, p => p.ServiceType == "qpAdm");
        Assert.Equal("qpAdm ancestry analysis", qpadm.DisplayName);
        Assert.Equal(49.99m, qpadm.BasePrice);
        Assert.Equal(3, qpadm.Addons.Count);
        Assert.Contains(qpadm.Addons, a => a.Code == "EXPEDITED" && a.Price == 20m);
        Assert.Contains(qpadm.Addons, a => a.Code == "Y_HAPLOGROUP" && a.Price == 20m);
        Assert.Contains(qpadm.Addons, a => a.Code == "MERGE_RAW" && a.Price == 40m);
    }

    [Fact]
    public async Task PreviewPrice_WithExpeditedAndWelcome10_ReturnsDiscountedTotal()
    {
        var catalogResponse = await Client.GetAsync("/api/catalog/products");
        catalogResponse.EnsureSuccessStatusCode();
        var products = await catalogResponse.Content.ReadFromJsonAsync<List<GetCatalogProductContract.ProductResponse>>(JsonOptions);
        Assert.NotNull(products);
        var expeditedId = products!.SelectMany(p => p.Addons).Single(a => a.Code == "EXPEDITED").Id;

        var previewRequest = new PreviewOrderPriceContract.Request
        {
            Service = Odin.Api.Data.Enums.OrderService.qpAdm,
            AddonIds = [expeditedId],
            PromoCode = "WELCOME10"
        };

        var json = JsonSerializer.Serialize(previewRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/api/catalog/preview-price", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var preview = await response.Content.ReadFromJsonAsync<PreviewOrderPriceContract.Response>(JsonOptions);
        Assert.NotNull(preview);
        Assert.Equal(49.99m, preview!.BasePrice);
        Assert.Single(preview.AddonLines);
        Assert.Equal(20m, preview.AddonLines[0].UnitPrice);
        Assert.Equal(69.99m, preview.SubtotalBeforeDiscount);
        Assert.Equal(7.00m, preview.DiscountAmount);
        Assert.Equal(62.99m, preview.Total);
    }

    [Fact]
    public async Task PreviewPrice_InvalidPromo_ReturnsBadRequest()
    {
        var previewRequest = new PreviewOrderPriceContract.Request
        {
            Service = Odin.Api.Data.Enums.OrderService.qpAdm,
            AddonIds = [],
            PromoCode = "NOPE"
        };

        var json = JsonSerializer.Serialize(previewRequest, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/api/catalog/preview-price", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
