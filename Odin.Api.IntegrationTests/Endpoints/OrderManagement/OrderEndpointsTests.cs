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

namespace Odin.Api.IntegrationTests.Endpoints.OrderManagement;

public class OrderEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // ── GET /api/orders ────────────────────────────────────────────

    [Fact]
    public async Task GetAll_ReturnsOk()
    {
        var response = await Client.GetAsync("/api/orders");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var orders = await response.Content.ReadFromJsonAsync<List<GetOrderContract.Response>>(JsonOptions);
        Assert.NotNull(orders);
    }

    [Fact]
    public async Task GetAll_AfterCreatingOrders_ReturnsAll()
    {
        await CreateOrderViaApiAsync(Client, Factory.Services);
        await CreateOrderViaApiAsync(Client, Factory.Services);

        var response = await Client.GetAsync("/api/orders");
        var orders = await response.Content.ReadFromJsonAsync<List<GetOrderContract.Response>>(JsonOptions);

        Assert.NotNull(orders);
        Assert.True(orders!.Count >= 2);
    }

    [Fact]
    public async Task GetAll_DifferentUser_DoesNotSeeOtherUsersOrders()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);

        var otherUser = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", otherUser);
        using var otherClient = CreateClientWithRole(Factory, otherUser.IdentityId, "User");

        var response = await otherClient.GetAsync("/api/orders");
        var orders = await response.Content.ReadFromJsonAsync<List<GetOrderContract.Response>>(JsonOptions);

        Assert.NotNull(orders);
        Assert.DoesNotContain(orders!, o => o.Id == created.Id);
    }

    // ── GET /api/orders/{id} ───────────────────────────────────────

    [Fact]
    public async Task GetById_WhenExists_ReturnsOrder()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);

        var response = await Client.GetAsync($"/api/orders/{created.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<GetOrderContract.Response>(JsonOptions);
        Assert.NotNull(order);
        Assert.Equal(created.Id, order!.Id);
        Assert.Equal("Pending", order.Status);
        Assert.NotEmpty(order.RegionIds);
        Assert.NotEmpty(order.EthnicityIds);
    }

    [Fact]
    public async Task GetById_WhenNotExists_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/orders/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetById_NonOwner_ReturnsNotFound()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);

        var otherUser = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", otherUser);
        using var otherClient = CreateClientWithRole(Factory, otherUser.IdentityId, "User");

        var response = await otherClient.GetAsync($"/api/orders/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── POST /api/orders (create) ──────────────────────────────────

    [Fact]
    public async Task Create_WithExpeditedAddon_ChargesBasePlusAddon()
    {
        var expeditedId = await GetAddonIdByCodeAsync(Client, "EXPEDITED");
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services);

        using var content = BuildOrderForm(regionIds, addonIds: [expeditedId]);
        var response = await Client.PostAsync("/api/orders", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(69.99m, created!.Price);
        Assert.Equal("qpAdm", created.Service);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lines = await db.OrderLineAddons.Where(l => l.OrderId == created.Id).ToListAsync();
        Assert.Single(lines);
        Assert.Equal(20m, lines[0].UnitPriceSnapshot);
    }

    [Fact]
    public async Task Create_WithoutAddons_UsesBasePriceOnly()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services);

        using var content = BuildOrderForm(regionIds);
        var response = await Client.PostAsync("/api/orders", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(49.99m, created!.Price);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var lines = await db.OrderLineAddons.Where(l => l.OrderId == created.Id).ToListAsync();
        Assert.Empty(lines);
    }

    [Fact]
    public async Task Create_WithAllThreeAddons_PriceSumsCorrectly()
    {
        var products = await GetCatalogProductsAsync(Client);
        var allAddonIds = products.SelectMany(p => p.Addons).Select(a => a.Id).ToList();
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services);

        using var content = BuildOrderForm(regionIds, addonIds: allAddonIds);
        var response = await Client.PostAsync("/api/orders", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(129.99m, created!.Price); // 49.99 + 20 + 20 + 40
    }

    [Fact]
    public async Task Create_WithPromoCode_AppliesDiscount()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services);

        using var content = BuildOrderForm(regionIds, promoCode: "WELCOME10");
        var response = await Client.PostAsync("/api/orders", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions);
        Assert.NotNull(created);
        Assert.Equal(44.99m, created!.Price); // 49.99 - 10% = 44.991 rounded to 44.99
    }

    [Fact]
    public async Task Create_WithPromoCode_IncrementsRedemptionCount()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services);

        await using var scopeBefore = Factory.Services.CreateAsyncScope();
        var dbBefore = scopeBefore.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var promoBefore = await dbBefore.PromoCodes.AsNoTracking().FirstAsync(p => p.Code == "WELCOME10");
        var countBefore = promoBefore.RedemptionCount;

        using var content = BuildOrderForm(regionIds, promoCode: "WELCOME10");
        var response = await Client.PostAsync("/api/orders", content);
        response.EnsureSuccessStatusCode();

        await using var scopeAfter = Factory.Services.CreateAsyncScope();
        var dbAfter = scopeAfter.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var promoAfter = await dbAfter.PromoCodes.AsNoTracking().FirstAsync(p => p.Code == "WELCOME10");
        Assert.Equal(countBefore + 1, promoAfter.RedemptionCount);
    }

    [Fact]
    public async Task Create_WithExistingFile_ReusesUploadedFile()
    {
        var fileId = await SeedRawGeneticFileAsync(Factory.Services, createdBy: "auth0|integration-default");
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Jane"), "FirstName");
        content.Add(new StringContent("Doe"), "LastName");
        content.Add(new StringContent("Female"), "Gender");
        content.Add(new StringContent("0"), "Service");
        foreach (var rid in regionIds)
            content.Add(new StringContent(rid.ToString()), "RegionIds");
        content.Add(new StringContent(fileId.ToString()), "ExistingFileId");

        var response = await Client.PostAsync("/api/orders", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithProfilePicture_StoresPicture()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services);

        using var content = BuildOrderForm(regionIds);
        var picBytes = ReportFaker.GeneratePngBytes(128);
        var picPart = new ByteArrayContent(picBytes);
        picPart.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        content.Add(picPart, "ProfilePicture", "avatar.png");

        var response = await Client.PostAsync("/api/orders", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions);

        var picResponse = await Client.GetAsync($"/api/orders/{created!.Id}/profile-picture");
        Assert.Equal(HttpStatusCode.OK, picResponse.StatusCode);
    }

    // ── Region validation ──────────────────────────────────────────

    [Fact]
    public async Task Create_WithMoreThan4Ethnicities_ReturnsBadRequest()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, ethnicityCount: 5, regionsPerEthnicity: 1);

        using var content = BuildOrderForm(regionIds);
        var response = await Client.PostAsync("/api/orders", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithMoreThan4RegionsPerEthnicity_ReturnsBadRequest()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, ethnicityCount: 1, regionsPerEthnicity: 5);

        using var content = BuildOrderForm(regionIds);
        var response = await Client.PostAsync("/api/orders", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithInvalidRegionIds_ReturnsBadRequest()
    {
        using var content = BuildOrderForm([99998, 99999]);
        var response = await Client.PostAsync("/api/orders", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_WithMissingLastName_ReturnsBadRequest()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Jane"), "FirstName");
        content.Add(new StringContent("Female"), "Gender");
        content.Add(new StringContent("0"), "Service");
        foreach (var rid in regionIds)
            content.Add(new StringContent(rid.ToString()), "RegionIds");

        var fileBytes = "rsid,chromosome,position,genotype\nrs1,1,1,AA\n"u8.ToArray();
        var filePart = new ByteArrayContent(fileBytes);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(filePart, "File", "kit.csv");

        var response = await Client.PostAsync("/api/orders", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── PUT /api/orders/{id} (update) ──────────────────────────────

    [Fact]
    public async Task Update_PendingOrder_Succeeds()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);
        var (newRegionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Updated"), "FirstName");
        content.Add(new StringContent("Name"), "LastName");
        foreach (var rid in newRegionIds)
            content.Add(new StringContent(rid.ToString()), "RegionIds");

        var response = await Client.PutAsync($"/api/orders/{created.Id}", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<GetOrderContract.Response>(JsonOptions);
        Assert.Equal("Updated", updated!.FirstName);
    }

    [Fact]
    public async Task Update_NonPendingOrder_ReturnsBadRequest()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);
        await SetOrderStatusAsync(Factory.Services, created.Id, OrderStatus.InProcess);

        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services);
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Updated"), "FirstName");
        content.Add(new StringContent("Name"), "LastName");
        foreach (var rid in regionIds)
            content.Add(new StringContent(rid.ToString()), "RegionIds");

        var response = await Client.PutAsync($"/api/orders/{created.Id}", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Update_NonExistentOrder_ReturnsNotFound()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, 1, 1);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Updated"), "FirstName");
        content.Add(new StringContent("Name"), "LastName");
        foreach (var rid in regionIds)
            content.Add(new StringContent(rid.ToString()), "RegionIds");

        var response = await Client.PutAsync("/api/orders/99999", content);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Update_NonOwner_ReturnsForbidden()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services);

        var otherUser = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", otherUser);
        using var otherClient = CreateClientWithRole(Factory, otherUser.IdentityId, "User");

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("Updated"), "FirstName");
        content.Add(new StringContent("Name"), "LastName");
        foreach (var rid in regionIds)
            content.Add(new StringContent(rid.ToString()), "RegionIds");

        var response = await otherClient.PutAsync($"/api/orders/{created.Id}", content);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── DELETE /api/orders/{id} ────────────────────────────────────

    [Fact]
    public async Task Delete_AsAdmin_ReturnsNoContent()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);

        var response = await Client.DeleteAsync($"/api/orders/{created.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await Client.GetAsync($"/api/orders/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_AsNonAdmin_ReturnsForbidden()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);

        using var userClient = CreateClientWithRole(Factory, "auth0|integration-default", "User");
        var response = await userClient.DeleteAsync($"/api/orders/{created.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Delete_NonExistentOrder_ReturnsNotFound()
    {
        var response = await Client.DeleteAsync("/api/orders/99999");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GET /api/orders/{id}/qpadm-result ──────────────────────────

    [Fact]
    public async Task GetQpadmResult_OwnerCompletedOrder_ReturnsOk()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);
        await SetOrderStatusAsync(Factory.Services, created.Id, OrderStatus.Completed);
        await SeedQpadmResultAsync(created.GeneticInspectionId);

        var response = await Client.GetAsync($"/api/orders/{created.Id}/qpadm-result");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<GetOrderQpadmResultContract.Response>(JsonOptions);
        Assert.NotNull(result);
        Assert.NotEmpty(result!.EraGroups);
    }

    [Fact]
    public async Task GetQpadmResult_NonOwner_ReturnsForbidden()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);
        await SetOrderStatusAsync(Factory.Services, created.Id, OrderStatus.Completed);

        var otherUser = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", otherUser);
        using var otherClient = CreateClientWithRole(Factory, otherUser.IdentityId, "Admin");

        var response = await otherClient.GetAsync($"/api/orders/{created.Id}/qpadm-result");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetQpadmResult_PendingOrder_ReturnsBadRequest()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);

        var response = await Client.GetAsync($"/api/orders/{created.Id}/qpadm-result");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetQpadmResult_NonExistentOrder_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/orders/99999/qpadm-result");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetQpadmResult_PopulationsIncludeMediaFileNames()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);
        await SetOrderStatusAsync(Factory.Services, created.Id, OrderStatus.Completed);
        await SeedQpadmResultAsync(created.GeneticInspectionId);

        var response = await Client.GetAsync($"/api/orders/{created.Id}/qpadm-result");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<GetOrderQpadmResultContract.Response>(JsonOptions);
        Assert.NotNull(result);

        var allPops = result!.EraGroups.SelectMany(eg => eg.Populations).ToList();
        Assert.NotEmpty(allPops);
        var withIcon = allPops.Where(p => p.IconFileName != null).ToList();
        Assert.True(withIcon.Count > 0, "At least one population should have an IconFileName");
        Assert.All(allPops, p => Assert.NotNull(p.MusicTrackFileName));
    }

    // ── PATCH /api/orders/{id}/viewed-status ───────────────────────

    [Fact]
    public async Task MarkViewed_OwnerOrder_ReturnsOk()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);

        var response = await Client.PatchAsync($"/api/orders/{created.Id}/viewed-status", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var order = await Client.GetAsync($"/api/orders/{created.Id}");
        var body = await order.Content.ReadFromJsonAsync<GetOrderContract.Response>(JsonOptions);
        Assert.True(body!.HasViewedResults);
    }

    [Fact]
    public async Task MarkViewed_NonOwner_ReturnsForbidden()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);

        var otherUser = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", otherUser);
        using var otherClient = CreateClientWithRole(Factory, otherUser.IdentityId, "Admin");

        var response = await otherClient.PatchAsync($"/api/orders/{created.Id}/viewed-status", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MarkViewed_NonExistentOrder_ReturnsNotFound()
    {
        var response = await Client.PatchAsync("/api/orders/99999/viewed-status", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── GET /api/orders/{id}/profile-picture ───────────────────────

    [Fact]
    public async Task GetProfilePicture_NoPicture_ReturnsNotFound()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);

        var response = await Client.GetAsync($"/api/orders/{created.Id}/profile-picture");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetProfilePicture_NonOwner_ReturnsForbidden()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);

        var otherUser = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", otherUser);
        using var otherClient = CreateClientWithRole(Factory, otherUser.IdentityId, "User");

        var response = await otherClient.GetAsync($"/api/orders/{created.Id}/profile-picture");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ── GET /api/orders/{id}/merged-data/download ──────────────────

    [Fact]
    public async Task DownloadMergedData_NonOwner_ReturnsForbidden()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);

        var otherUser = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", otherUser);
        using var otherClient = CreateClientWithRole(Factory, otherUser.IdentityId, "Admin");

        var response = await otherClient.GetAsync($"/api/orders/{created.Id}/merged-data/download");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DownloadMergedData_NoMergedData_ReturnsNotFound()
    {
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);

        var response = await Client.GetAsync($"/api/orders/{created.Id}/merged-data/download");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── Authorization ──────────────────────────────────────────────

    [Fact]
    public async Task Create_Unauthenticated_ReturnsUnauthorized()
    {
        using var unauthClient = CreateUnauthenticatedClient(Factory);
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services);

        using var content = BuildOrderForm(regionIds);
        var response = await unauthClient.PostAsync("/api/orders", content);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ── Helpers ────────────────────────────────────────────────────

    private static MultipartFormDataContent BuildOrderForm(
        List<int> regionIds,
        List<int>? addonIds = null,
        string? promoCode = null,
        string firstName = "Jane",
        string lastName = "Doe",
        string gender = "Female")
    {
        var content = new MultipartFormDataContent();
        content.Add(new StringContent(firstName), "FirstName");
        content.Add(new StringContent(lastName), "LastName");
        content.Add(new StringContent(gender), "Gender");
        content.Add(new StringContent("0"), "Service");

        foreach (var rid in regionIds)
            content.Add(new StringContent(rid.ToString()), "RegionIds");

        if (addonIds is not null)
            foreach (var id in addonIds)
                content.Add(new StringContent(id.ToString()), "AddonIds");

        if (promoCode is not null)
            content.Add(new StringContent(promoCode), "PromoCode");

        var fileBytes = "rsid,chromosome,position,genotype\nrs1,1,1,AA\n"u8.ToArray();
        var filePart = new ByteArrayContent(fileBytes);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(filePart, "File", "kit.csv");

        return content;
    }

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
            PValue = 0.05m,
            RightSources = "WHG, EHG"
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
