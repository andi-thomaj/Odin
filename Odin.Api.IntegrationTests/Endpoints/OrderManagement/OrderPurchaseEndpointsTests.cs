using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.OrderManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;
using static Odin.Api.IntegrationTests.Fakers.TestDataHelper;

namespace Odin.Api.IntegrationTests.Endpoints.OrderManagement;

/// <summary>
/// The iOS in-app-purchase paid-order flow: <c>POST /orders/purchase</c> validates the StoreKit
/// transaction (signature checks are skipped under Testing), creates the order, records the consumed
/// transaction, and is idempotent on the Apple transaction id. The free <c>POST /orders</c> path is open to any
/// authenticated + email-verified user (simple users included) so regular web accounts can create orders.
/// </summary>
public class OrderPurchaseEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task CreatePurchase_Qpadm_CreatesPaidOrder_AndRecordsConsumedTransaction()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, 1, 1);
        var transactionId = Guid.NewGuid().ToString("N");

        var response = await PostQpadmPurchaseAsync(regionIds, transactionId);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions))!;
        Assert.Equal("qpAdm", order.Service);
        Assert.Equal(49.90m, order.Price);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var txn = await db.AppStoreTransactions.SingleAsync(t => t.TransactionId == transactionId);
        Assert.Equal(AppStoreTransactionStatus.Consumed, txn.Status);
        Assert.Equal(ServiceType.qpAdm, txn.Service);
        Assert.Equal(order.Id, txn.QpadmOrderId);
        Assert.Null(txn.G25OrderId);
    }

    [Fact]
    public async Task CreatePurchase_G25WithCoordinates_CreatesPaidOrder()
    {
        var transactionId = Guid.NewGuid().ToString("N");
        using var content = new MultipartFormDataContent
        {
            { new StringContent("Ada"), "FirstName" },
            { new StringContent("Lovelace"), "LastName" },
            { new StringContent("Female"), "Gender" },
            { new StringContent("1"), "Service" },
            { new StringContent("Target,0.1,0.2,0.3"), "G25Coordinates" },
            { new StringContent(BuildAppStoreTransactionJws(ServiceType.g25, transactionId)), "AppStoreTransaction" },
        };

        var response = await Client.PostAsync("/api/orders/purchase", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions))!;
        Assert.Equal("g25", order.Service);
        Assert.Equal(39.90m, order.Price);
    }

    [Fact]
    public async Task CreatePurchase_ReplayingSameTransaction_ReturnsSameOrder()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, 1, 1);
        var transactionId = Guid.NewGuid().ToString("N");

        var first = await PostQpadmPurchaseAsync(regionIds, transactionId);
        var second = await PostQpadmPurchaseAsync(regionIds, transactionId);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstOrder = (await first.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions))!;
        var secondOrder = (await second.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions))!;

        // Idempotent: the replay returns the SAME order rather than creating a second one.
        Assert.Equal(firstOrder.Id, secondOrder.Id);

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.AppStoreTransactions.CountAsync(t => t.TransactionId == transactionId));
        Assert.Equal(1, await db.QpadmOrders.CountAsync(o => o.Id == firstOrder.Id));
    }

    [Fact]
    public async Task CreatePurchase_MissingTransaction_ReturnsBadRequest()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, 1, 1);
        using var content = QpadmForm(regionIds);
        // No AppStoreTransaction field.
        var response = await Client.PostAsync("/api/orders/purchase", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreatePurchase_ProductServiceMismatch_ReturnsBadRequest()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, 1, 1);
        using var content = QpadmForm(regionIds);
        // qpAdm order, but a G25 product transaction — must be rejected.
        content.Add(new StringContent(BuildAppStoreTransactionJws(ServiceType.g25)), "AppStoreTransaction");
        var response = await Client.PostAsync("/api/orders/purchase", content);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateFree_AsNonAdmin_IsAllowed()
    {
        // The free endpoint is open to any verified user now — a regular (non-admin) account can create an order.
        var userClient = await CreateClientAsAsync("auth0|order-lockdown-user", AppRole.User);
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, 1, 1);
        using var content = QpadmForm(regionIds);
        var response = await userClient.PostAsync("/api/orders", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateFree_AsAdmin_StillWorks()
    {
        // Admins can still create free orders via the legacy endpoint (support/back-office path).
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, 1, 1);
        using var content = QpadmForm(regionIds);
        var response = await Client.PostAsync("/api/orders", content); // Client is Admin
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions))!;
        Assert.Equal(0m, order.Price); // free path leaves price at 0
    }

    private Task<HttpResponseMessage> PostQpadmPurchaseAsync(List<int> regionIds, string transactionId)
    {
        var content = QpadmForm(regionIds);
        content.Add(new StringContent(BuildAppStoreTransactionJws(ServiceType.qpAdm, transactionId)), "AppStoreTransaction");
        return Client.PostAsync("/api/orders/purchase", content);
    }

    private static MultipartFormDataContent QpadmForm(List<int> regionIds)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent("Ada"), "FirstName" },
            { new StringContent("Lovelace"), "LastName" },
            { new StringContent("Female"), "Gender" },
            { new StringContent("0"), "Service" },
        };
        foreach (var regionId in regionIds)
            content.Add(new StringContent(regionId.ToString()), "RegionIds");

        var fileBytes = "rsid,chromosome,position,genotype\nrs1,1,1,AA\n"u8.ToArray();
        var filePart = new ByteArrayContent(fileBytes);
        filePart.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        content.Add(filePart, "File", "kit.csv");
        return content;
    }
}
