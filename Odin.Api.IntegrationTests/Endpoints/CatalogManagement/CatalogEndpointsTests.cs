using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.CatalogManagement.Models;
using Odin.Api.IntegrationTests.Fakers;
using Odin.Api.IntegrationTests.Infrastructure;
using static Odin.Api.IntegrationTests.Fakers.TestDataHelper;

namespace Odin.Api.IntegrationTests.Endpoints.CatalogManagement;

public class CatalogEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── GET /api/catalog/products ──────────────────────────────────

    [Fact]
    public async Task GetProducts_ReturnsQpadmWithThreeAddons()
    {
        var response = await Client.GetAsync("/api/catalog/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var products = await response.Content.ReadFromJsonAsync<List<GetCatalogProductContract.ProductResponse>>(JsonOptions);
        Assert.NotNull(products);
        var qpadm = Assert.Single(products!, p => p.ServiceType == "qpAdm");
        Assert.Equal("qpAdm ancestry analysis", qpadm.DisplayName);
        Assert.Equal(49.99m, qpadm.BasePrice);
        Assert.Equal(3, qpadm.Addons.Count);
        Assert.Contains(qpadm.Addons, a => a.Code == "EXPEDITED" && a.Price == 20m);
        Assert.Contains(qpadm.Addons, a => a.Code == "Y_HAPLOGROUP" && a.Price == 20m);
        Assert.Contains(qpadm.Addons, a => a.Code == "MERGE_RAW" && a.Price == 40m);
    }

    // ── POST /api/catalog/preview-price ────────────────────────────

    [Fact]
    public async Task PreviewPrice_NoAddons_ReturnsBaseOnly()
    {
        var preview = await PostPreviewAsync(new PreviewOrderPriceContract.Request
        {
            Service = ServiceType.qpAdm,
            AddonIds = [],
            PromoCode = null
        });

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var body = await preview.Content.ReadFromJsonAsync<PreviewOrderPriceContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(49.99m, body!.BasePrice);
        Assert.Empty(body.AddonLines);
        Assert.Equal(49.99m, body.SubtotalBeforeDiscount);
        Assert.Equal(0m, body.DiscountAmount);
        Assert.Equal(49.99m, body.Total);
    }

    [Fact]
    public async Task PreviewPrice_AllThreeAddons_SumsCorrectly()
    {
        var products = await GetCatalogProductsAsync(Client);
        var allAddonIds = products.SelectMany(p => p.Addons).Select(a => a.Id).ToList();

        var preview = await PostPreviewAsync(new PreviewOrderPriceContract.Request
        {
            Service = ServiceType.qpAdm,
            AddonIds = allAddonIds
        });

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var body = await preview.Content.ReadFromJsonAsync<PreviewOrderPriceContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(49.99m, body!.BasePrice);
        Assert.Equal(3, body.AddonLines.Count);
        Assert.Equal(129.99m, body.SubtotalBeforeDiscount); // 49.99 + 20 + 20 + 40
        Assert.Equal(129.99m, body.Total);
    }

    [Fact]
    public async Task PreviewPrice_WithExpeditedAndWelcome10_ReturnsDiscountedTotal()
    {
        var expeditedId = await GetAddonIdByCodeAsync(Client, "EXPEDITED");

        var preview = await PostPreviewAsync(new PreviewOrderPriceContract.Request
        {
            Service = ServiceType.qpAdm,
            AddonIds = [expeditedId],
            PromoCode = "WELCOME10"
        });

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var body = await preview.Content.ReadFromJsonAsync<PreviewOrderPriceContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(49.99m, body!.BasePrice);
        Assert.Single(body.AddonLines);
        Assert.Equal(20m, body.AddonLines[0].UnitPrice);
        Assert.Equal(69.99m, body.SubtotalBeforeDiscount);
        Assert.Equal(7.00m, body.DiscountAmount);
        Assert.Equal(62.99m, body.Total);
    }

    [Fact]
    public async Task PreviewPrice_InvalidPromo_ReturnsBadRequest()
    {
        var preview = await PostPreviewAsync(new PreviewOrderPriceContract.Request
        {
            Service = ServiceType.qpAdm,
            AddonIds = [],
            PromoCode = "NOPE"
        });

        Assert.Equal(HttpStatusCode.BadRequest, preview.StatusCode);
    }

    [Fact]
    public async Task PreviewPrice_ExpiredPromo_ReturnsBadRequest()
    {
        await SeedPromoCodeAsync(Factory.Services, "EXPIRED10",
            validUntilUtc: DateTime.UtcNow.AddDays(-1),
            applicableService: ServiceType.qpAdm);

        var preview = await PostPreviewAsync(new PreviewOrderPriceContract.Request
        {
            Service = ServiceType.qpAdm,
            PromoCode = "EXPIRED10"
        });

        Assert.Equal(HttpStatusCode.BadRequest, preview.StatusCode);
    }

    [Fact]
    public async Task PreviewPrice_PromoNotYetActive_ReturnsBadRequest()
    {
        await SeedPromoCodeAsync(Factory.Services, "FUTURE10",
            validFromUtc: DateTime.UtcNow.AddDays(30),
            applicableService: ServiceType.qpAdm);

        var preview = await PostPreviewAsync(new PreviewOrderPriceContract.Request
        {
            Service = ServiceType.qpAdm,
            PromoCode = "FUTURE10"
        });

        Assert.Equal(HttpStatusCode.BadRequest, preview.StatusCode);
    }

    [Fact]
    public async Task PreviewPrice_PromoAtRedemptionLimit_ReturnsBadRequest()
    {
        await SeedPromoCodeAsync(Factory.Services, "MAXED10",
            maxRedemptions: 5,
            redemptionCount: 5,
            applicableService: ServiceType.qpAdm);

        var preview = await PostPreviewAsync(new PreviewOrderPriceContract.Request
        {
            Service = ServiceType.qpAdm,
            PromoCode = "MAXED10"
        });

        Assert.Equal(HttpStatusCode.BadRequest, preview.StatusCode);
    }

    [Fact]
    public async Task PreviewPrice_InvalidAddonIds_ReturnsBadRequest()
    {
        var preview = await PostPreviewAsync(new PreviewOrderPriceContract.Request
        {
            Service = ServiceType.qpAdm,
            AddonIds = [99999]
        });

        Assert.Equal(HttpStatusCode.BadRequest, preview.StatusCode);
    }

    [Fact]
    public async Task PreviewPrice_FixedAmountPromo_CapsDiscountAtSubtotal()
    {
        await SeedPromoCodeAsync(Factory.Services, "FIXED999",
            discountType: PromoDiscountType.FixedAmount,
            value: 999m,
            applicableService: ServiceType.qpAdm);

        var preview = await PostPreviewAsync(new PreviewOrderPriceContract.Request
        {
            Service = ServiceType.qpAdm,
            AddonIds = [],
            PromoCode = "FIXED999"
        });

        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var body = await preview.Content.ReadFromJsonAsync<PreviewOrderPriceContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.Equal(49.99m, body!.DiscountAmount);
        Assert.Equal(0m, body.Total);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> PostPreviewAsync(PreviewOrderPriceContract.Request request)
    {
        var json = JsonSerializer.Serialize(request, WriteOptions);
        using var httpContent = new StringContent(json, Encoding.UTF8, "application/json");
        return await Client.PostAsync("/api/catalog/preview-price", httpContent);
    }
}
