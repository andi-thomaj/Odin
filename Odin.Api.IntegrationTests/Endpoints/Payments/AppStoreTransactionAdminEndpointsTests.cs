using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.Payments.Models;
using Odin.Api.IntegrationTests.Infrastructure;
using static Odin.Api.IntegrationTests.Fakers.TestDataHelper;

namespace Odin.Api.IntegrationTests.Endpoints.Payments;

/// <summary>
/// The admin "App Store Transactions" feed (<c>GET /api/admin/app-store-transactions</c>) is the unified
/// back-office view of EVERY purchase type: paid qpAdm/G25 analysis orders plus the per-order add-ons
/// (Y-DNA unlock + "Through the Ages" AI portraits). Each row carries the nominal money paid + currency.
/// </summary>
public class AppStoreTransactionAdminEndpointsTests(CustomWebApplicationFactory factory)
    : IntegrationTestBase(factory)
{
    private const string DefaultIdentity = "auth0|integration-default"; // the base Admin client's identity

    [Fact]
    public async Task Feed_IncludesAllPurchaseKinds_WithNominalAmounts()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, 1, 1);
        var orderTxnId = Guid.NewGuid().ToString("N");

        // 1. A real paid qpAdm order + a real paid G25 order (each records an app_store_transactions row).
        var purchase = await PostQpadmPurchaseAsync(regionIds, orderTxnId);
        Assert.Equal(HttpStatusCode.Created, purchase.StatusCode);
        var order = (await purchase.Content.ReadFromJsonAsync<Odin.Api.Endpoints.OrderManagement.Models.CreateOrderContract.Response>(JsonOptions))!;

        var g25TxnId = Guid.NewGuid().ToString("N");
        var g25Purchase = await PostG25PurchaseAsync(g25TxnId);
        Assert.Equal(HttpStatusCode.Created, g25Purchase.StatusCode);
        var g25Order = (await g25Purchase.Content.ReadFromJsonAsync<Odin.Api.Endpoints.OrderManagement.Models.CreateOrderContract.Response>(JsonOptions))!;

        // 2. Directly seed the two add-on entitlements, owned by the same (provisioned) admin user.
        var ydnaTxnId = Guid.NewGuid().ToString("N");
        var portraitTxnId = Guid.NewGuid().ToString("N");
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.IdentityId == DefaultIdentity);
            var now = DateTime.UtcNow;

            db.QpadmYDnaUnlocks.Add(new QpadmYDnaUnlock
            {
                OrderId = order.Id,
                UserId = user.Id,
                TransactionId = ydnaTxnId,
                CreatedBy = DefaultIdentity,
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.AncestralPortraitSets.Add(new AncestralPortraitSet
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                UserId = user.Id,
                TransactionId = portraitTxnId,
                Status = AncestralPortraitStatus.Succeeded,
                CreatedBy = DefaultIdentity,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        // 3. The admin feed returns all three, newest-first, each with its nominal amount + currency.
        var rows = (await Client.GetFromJsonAsync<List<AdminAppStoreTransactionContract.Response>>(
            "/api/admin/app-store-transactions", JsonOptions))!;

        var orderRow = Assert.Single(rows, r => r.TransactionId == orderTxnId);
        Assert.Equal("Order", orderRow.Kind);
        Assert.Equal("qpAdm Analysis", orderRow.ProductLabel);
        Assert.Equal("qpAdm", orderRow.Service);
        Assert.Equal(49.90m, orderRow.Amount);
        Assert.Equal("EUR", orderRow.Currency);
        Assert.Equal(order.Id, orderRow.QpadmOrderId);
        Assert.Equal("Consumed", orderRow.Status);
        Assert.Equal("integration-default@test.local", orderRow.OwnerEmail);

        var g25Row = Assert.Single(rows, r => r.TransactionId == g25TxnId);
        Assert.Equal("Order", g25Row.Kind);
        Assert.Equal("G25 Analysis", g25Row.ProductLabel);
        Assert.Equal("g25", g25Row.Service);
        Assert.Equal(39.90m, g25Row.Amount);
        Assert.Equal("EUR", g25Row.Currency);
        Assert.Equal(g25Order.Id, g25Row.G25OrderId);
        Assert.Null(g25Row.QpadmOrderId);

        var ydnaRow = Assert.Single(rows, r => r.TransactionId == ydnaTxnId);
        Assert.Equal("YDnaUnlock", ydnaRow.Kind);
        Assert.Equal("Y-DNA Unlock", ydnaRow.ProductLabel);
        Assert.Equal(9.99m, ydnaRow.Amount);
        Assert.Equal("EUR", ydnaRow.Currency);
        Assert.Equal(order.Id, ydnaRow.QpadmOrderId);

        var portraitRow = Assert.Single(rows, r => r.TransactionId == portraitTxnId);
        Assert.Equal("AiPortraits", portraitRow.Kind);
        Assert.Equal("Through the Ages", portraitRow.ProductLabel);
        Assert.Equal(9.99m, portraitRow.Amount);
        Assert.Equal("EUR", portraitRow.Currency);
        Assert.Equal(order.Id, portraitRow.QpadmOrderId);

        // Stable, collision-proof row keys across kinds.
        Assert.Equal(rows.Count, rows.Select(r => r.RowKey).Distinct().Count());
    }

    [Fact]
    public async Task Feed_ReflectsRefunds_AndUnprovisionedOwner()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, 1, 1);
        var refundedOrderTxn = Guid.NewGuid().ToString("N");

        // A paid order that Apple later refunds (the webhook stamps the txn Refunded).
        var purchase = await PostQpadmPurchaseAsync(regionIds, refundedOrderTxn);
        Assert.Equal(HttpStatusCode.Created, purchase.StatusCode);
        var order = (await purchase.Content.ReadFromJsonAsync<Odin.Api.Endpoints.OrderManagement.Models.CreateOrderContract.Response>(JsonOptions))!;

        var refundedPortraitTxn = Guid.NewGuid().ToString("N");
        var ghostTxn = Guid.NewGuid().ToString("N");
        const string ghostIdentity = "auth0|never-provisioned-ghost";

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.IdentityId == DefaultIdentity);
            var now = DateTime.UtcNow;

            // Refund the order's transaction (what AppStoreWebhookEndpoints does on REFUND/REVOKE).
            var txn = await db.AppStoreTransactions.SingleAsync(t => t.TransactionId == refundedOrderTxn);
            txn.Status = AppStoreTransactionStatus.Refunded;

            // A refunded AI-portraits set keeps its row (images preserved) but carries RefundedAt.
            db.AncestralPortraitSets.Add(new AncestralPortraitSet
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                UserId = user.Id,
                TransactionId = refundedPortraitTxn,
                Status = AncestralPortraitStatus.Succeeded,
                RefundedAt = now,
                CreatedBy = DefaultIdentity,
                CreatedAt = now,
                UpdatedAt = now,
            });

            // A transaction whose creator was never provisioned into application_users — must still appear, with a null owner.
            db.AppStoreTransactions.Add(new AppStoreTransaction
            {
                TransactionId = ghostTxn,
                OriginalTransactionId = ghostTxn,
                ProductId = "io.ancestrify.app.qpadm",
                Service = ServiceType.qpAdm,
                Status = AppStoreTransactionStatus.Consumed,
                QpadmOrderId = order.Id,
                PurchaseDate = now,
                Environment = "Production",
                CreatedBy = ghostIdentity,
                CreatedAt = now,
                UpdatedAt = now,
            });

            await db.SaveChangesAsync();
        }

        var rows = (await Client.GetFromJsonAsync<List<AdminAppStoreTransactionContract.Response>>(
            "/api/admin/app-store-transactions", JsonOptions))!;

        // Refunded order surfaces with Status=Refunded but keeps its amount (the FE strikes it through + drops it from net).
        var refundedOrderRow = Assert.Single(rows, r => r.TransactionId == refundedOrderTxn);
        Assert.Equal("Refunded", refundedOrderRow.Status);
        Assert.Equal(49.90m, refundedOrderRow.Amount);

        // Refunded AI-portraits set surfaces as Refunded (RefundedAt set) rather than the default Consumed.
        var refundedPortraitRow = Assert.Single(rows, r => r.TransactionId == refundedPortraitTxn);
        Assert.Equal("AiPortraits", refundedPortraitRow.Kind);
        Assert.Equal("Refunded", refundedPortraitRow.Status);

        // Unprovisioned creator → row present with a null owner (documented left-join contract).
        var ghostRow = Assert.Single(rows, r => r.TransactionId == ghostTxn);
        Assert.Null(ghostRow.OwnerId);
        Assert.Null(ghostRow.OwnerEmail);
        Assert.Equal(string.Empty, ghostRow.OwnerFirstName);
        Assert.Equal(string.Empty, ghostRow.OwnerLastName);
        Assert.Equal(ghostIdentity, ghostRow.CreatedBy);
    }

    [Fact]
    public async Task Feed_AsNonAdmin_IsForbidden()
    {
        var userClient = await CreateClientAsAsync("auth0|app-store-feed-user", AppRole.User);
        var response = await userClient.GetAsync("/api/admin/app-store-transactions");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private Task<HttpResponseMessage> PostQpadmPurchaseAsync(List<int> regionIds, string transactionId)
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

        content.Add(new StringContent(BuildAppStoreTransactionJws(ServiceType.qpAdm, transactionId)), "AppStoreTransaction");
        return Client.PostAsync("/api/orders/purchase", content);
    }

    private Task<HttpResponseMessage> PostG25PurchaseAsync(string transactionId)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent("Ada"), "FirstName" },
            { new StringContent("Lovelace"), "LastName" },
            { new StringContent("Female"), "Gender" },
            { new StringContent("1"), "Service" },
            { new StringContent("Target,0.1,0.2,0.3"), "G25Coordinates" },
            { new StringContent(BuildAppStoreTransactionJws(ServiceType.g25, transactionId)), "AppStoreTransaction" },
        };
        return Client.PostAsync("/api/orders/purchase", content);
    }
}
