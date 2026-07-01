using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.OrderManagement.Models;
using Odin.Api.Endpoints.Payments;
using Odin.Api.IntegrationTests.Infrastructure;
using Odin.Api.Storage;
using static Odin.Api.IntegrationTests.Fakers.TestDataHelper;

namespace Odin.Api.IntegrationTests.Endpoints.Payments;

/// <summary>
/// When Apple refunds a paid qpAdm/G25 order, <see cref="IRefundCleanupJob"/> purges the order and everything
/// generated from it — the genetic inspection + results (EF cascade), the per-order add-ons (Y-DNA unlock +
/// "Through the Ages" AI-portrait sets incl. their private R2 images), and the raw DNA file + tools-api merge bundle
/// (only when no other order still references the file) — while KEEPING the <c>app_store_transactions</c> audit row.
/// </summary>
public class RefundCleanupJobTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private const string DefaultIdentity = "auth0|integration-default";

    [Fact]
    public async Task Purge_DeletesQpadmOrder_Results_AddOns_AndR2_KeepsTxnRow()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, 1, 1);
        var orderTxnId = Guid.NewGuid().ToString("N");

        var order = await CreatePaidQpadmOrderAsync(regionIds, orderTxnId);

        // Seed both add-ons for the order: an AI-portrait set with two portrait images (uploaded to R2) + a Y-DNA unlock.
        var portraitKeys = new[]
        {
            $"users/{DefaultIdentity}/ancestral-portraits/{Guid.NewGuid():N}/1-1-0.jpg",
            $"users/{DefaultIdentity}/ancestral-portraits/{Guid.NewGuid():N}/1-2-0.jpg",
        };
        int rawFileId;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var r2 = scope.ServiceProvider.GetRequiredService<IR2Storage>();
            var user = await db.Users.SingleAsync(u => u.IdentityId == DefaultIdentity);
            rawFileId = (await db.QpadmGeneticInspections.SingleAsync(gi => gi.OrderId == order.Id)).RawGeneticFileId;
            var now = DateTime.UtcNow;

            foreach (var key in portraitKeys)
                await r2.UploadAsync(key, new MemoryStream(Encoding.UTF8.GetBytes("img")), "image/jpeg");

            var set = new AncestralPortraitSet
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                UserId = user.Id,
                TransactionId = Guid.NewGuid().ToString("N"),
                Status = AncestralPortraitStatus.Succeeded,
                CreatedBy = DefaultIdentity,
                CreatedAt = now,
                UpdatedAt = now,
                Portraits = portraitKeys.Select((key, i) => new AncestralPortrait
                {
                    EraId = 1,
                    EraName = "Bronze Age",
                    PopulationId = i + 1,
                    PopulationName = "Pop",
                    R2Key = key,
                    ContentType = "image/jpeg",
                    IsSelected = true,
                    CreatedBy = DefaultIdentity,
                    CreatedAt = now,
                    UpdatedAt = now,
                }).ToList(),
            };
            db.AncestralPortraitSets.Add(set);
            db.QpadmYDnaUnlocks.Add(new QpadmYDnaUnlock
            {
                OrderId = order.Id,
                UserId = user.Id,
                TransactionId = Guid.NewGuid().ToString("N"),
                CreatedBy = DefaultIdentity,
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        // Act — run the purge the refund webhook would enqueue.
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<IRefundCleanupJob>();
            await job.PurgeRefundedOrderAsync(ServiceType.qpAdm, order.Id);
        }

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var r2 = scope.ServiceProvider.GetRequiredService<IR2Storage>();

            // Order + inspection (and any cascaded results/clade/regions) are gone.
            Assert.False(await db.QpadmOrders.AnyAsync(o => o.Id == order.Id));
            Assert.False(await db.QpadmGeneticInspections.AnyAsync(gi => gi.OrderId == order.Id));

            // Add-ons gone — rows AND the private R2 portrait images.
            Assert.False(await db.AncestralPortraitSets.AnyAsync(s => s.OrderId == order.Id));
            Assert.False(await db.AncestralPortraits.AnyAsync());
            Assert.False(await db.QpadmYDnaUnlocks.AnyAsync(u => u.OrderId == order.Id));
            foreach (var key in portraitKeys)
                Assert.Null(await r2.DownloadAsync(key));

            // The unshared raw DNA file is soft-deleted (hidden by the global filter).
            var file = await db.RawGeneticFiles.IgnoreQueryFilters().SingleAsync(f => f.Id == rawFileId);
            Assert.True(file.IsDeleted);

            // The financial audit row stays (the webhook marks it Refunded; the purge never deletes it).
            Assert.True(await db.AppStoreTransactions.AnyAsync(t => t.TransactionId == orderTxnId));
        }
    }

    [Fact]
    public async Task Purge_SharedRawFile_IsPreserved_AndSiblingOrderSurvives()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, 1, 1);

        var first = await CreatePaidQpadmOrderAsync(regionIds, Guid.NewGuid().ToString("N"));

        int sharedFileId;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            sharedFileId = (await db.QpadmGeneticInspections.SingleAsync(gi => gi.OrderId == first.Id)).RawGeneticFileId;
        }

        // A SECOND order reusing the SAME uploaded file (a kit can back several orders).
        var second = await CreatePaidQpadmOrderReusingFileAsync(regionIds, sharedFileId, Guid.NewGuid().ToString("N"));

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<IRefundCleanupJob>();
            await job.PurgeRefundedOrderAsync(ServiceType.qpAdm, first.Id);
        }

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.QpadmOrders.AnyAsync(o => o.Id == first.Id));     // refunded order gone
            Assert.True(await db.QpadmOrders.AnyAsync(o => o.Id == second.Id));     // sibling survives

            // The shared file is NOT soft-deleted — the sibling order still needs it.
            var file = await db.RawGeneticFiles.IgnoreQueryFilters().SingleAsync(f => f.Id == sharedFileId);
            Assert.False(file.IsDeleted);
            Assert.True(await db.QpadmGeneticInspections.AnyAsync(gi => gi.OrderId == second.Id));
        }
    }

    [Fact]
    public async Task Purge_G25Order_DeletesOrderAndResults()
    {
        var g25TxnId = Guid.NewGuid().ToString("N");
        var order = await CreatePaidG25OrderAsync(g25TxnId);

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<IRefundCleanupJob>();
            await job.PurgeRefundedOrderAsync(ServiceType.g25, order.Id);
        }

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.G25Orders.AnyAsync(o => o.Id == order.Id));
            Assert.False(await db.G25GeneticInspections.AnyAsync(gi => gi.OrderId == order.Id));
            Assert.True(await db.AppStoreTransactions.AnyAsync(t => t.TransactionId == g25TxnId));
        }
    }

    [Fact]
    public async Task Purge_WithMergeBundle_DeletesBundle_AndSoftDeletesFile()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, 1, 1);
        var order = await CreatePaidQpadmOrderAsync(regionIds, Guid.NewGuid().ToString("N"));

        int rawFileId;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var file = await db.RawGeneticFiles
                .SingleAsync(f => f.Id == db.QpadmGeneticInspections.Single(gi => gi.OrderId == order.Id).RawGeneticFileId);
            rawFileId = file.Id;
            // Pretend a merge bundle was produced for this kit.
            file.MergeId = $"merge-{Guid.NewGuid():N}";
            file.MergeStatus = MergeStatus.Ready;
            await db.SaveChangesAsync();
        }

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<IRefundCleanupJob>();
            await job.PurgeRefundedOrderAsync(ServiceType.qpAdm, order.Id);
        }

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var file = await db.RawGeneticFiles.IgnoreQueryFilters().SingleAsync(f => f.Id == rawFileId);
            Assert.True(file.IsDeleted);                          // file soft-deleted
            Assert.Equal(MergeStatus.Deleted, file.MergeStatus);  // tools-api bundle dropped
        }
    }

    [Fact]
    public async Task Purge_DeletesPortraitSets_RegardlessOfStatus()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, 1, 1);
        var order = await CreatePaidQpadmOrderAsync(regionIds, Guid.NewGuid().ToString("N"));

        // A refund overrides the normal "don't delete a set mid-generation" guard — every set goes, any status.
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var user = await db.Users.SingleAsync(u => u.IdentityId == DefaultIdentity);
            var now = DateTime.UtcNow;
            foreach (var status in new[] { AncestralPortraitStatus.Running, AncestralPortraitStatus.Failed, AncestralPortraitStatus.Pending })
            {
                db.AncestralPortraitSets.Add(new AncestralPortraitSet
                {
                    Id = Guid.NewGuid(),
                    OrderId = order.Id,
                    UserId = user.Id,
                    TransactionId = Guid.NewGuid().ToString("N"),
                    Status = status,
                    CreatedBy = DefaultIdentity,
                    CreatedAt = now,
                    UpdatedAt = now, // a FRESH Running set (would 409 the user-facing delete) — the purge still removes it
                });
            }
            await db.SaveChangesAsync();
        }

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<IRefundCleanupJob>();
            await job.PurgeRefundedOrderAsync(ServiceType.qpAdm, order.Id);
        }

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.AncestralPortraitSets.AnyAsync(s => s.OrderId == order.Id));
        }
    }

    [Fact]
    public async Task Purge_IsIdempotent()
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, 1, 1);
        var order = await CreatePaidQpadmOrderAsync(regionIds, Guid.NewGuid().ToString("N"));

        int rawFileId;
        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            rawFileId = (await db.QpadmGeneticInspections.SingleAsync(gi => gi.OrderId == order.Id)).RawGeneticFileId;
        }

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var job = scope.ServiceProvider.GetRequiredService<IRefundCleanupJob>();
            await job.PurgeRefundedOrderAsync(ServiceType.qpAdm, order.Id);
            // A second run (e.g. a re-delivered notification) must no-op, not throw.
            await job.PurgeRefundedOrderAsync(ServiceType.qpAdm, order.Id);
        }

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.QpadmOrders.AnyAsync(o => o.Id == order.Id));
            // The file stays soft-deleted across re-runs (the second run finds it already IsDeleted and no-ops).
            var file = await db.RawGeneticFiles.IgnoreQueryFilters().SingleAsync(f => f.Id == rawFileId);
            Assert.True(file.IsDeleted);
        }
    }

    private async Task<CreateOrderContract.Response> CreatePaidQpadmOrderAsync(List<int> regionIds, string transactionId)
    {
        var content = QpadmForm(regionIds);
        content.Add(new StringContent(BuildAppStoreTransactionJws(ServiceType.qpAdm, transactionId)), "AppStoreTransaction");
        var response = await Client.PostAsync("/api/orders/purchase", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions))!;
    }

    private async Task<CreateOrderContract.Response> CreatePaidQpadmOrderReusingFileAsync(
        List<int> regionIds, int existingFileId, string transactionId)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent("Ada"), "FirstName" },
            { new StringContent("Lovelace"), "LastName" },
            { new StringContent("Female"), "Gender" },
            { new StringContent("0"), "Service" },
            { new StringContent(existingFileId.ToString()), "ExistingFileId" },
            { new StringContent(BuildAppStoreTransactionJws(ServiceType.qpAdm, transactionId)), "AppStoreTransaction" },
        };
        foreach (var regionId in regionIds)
            content.Add(new StringContent(regionId.ToString()), "RegionIds");
        var response = await Client.PostAsync("/api/orders/purchase", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions))!;
    }

    private async Task<CreateOrderContract.Response> CreatePaidG25OrderAsync(string transactionId)
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
        var response = await Client.PostAsync("/api/orders/purchase", content);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions))!;
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
