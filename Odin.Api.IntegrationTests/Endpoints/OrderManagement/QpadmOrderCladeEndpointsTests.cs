using System.Net;
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

/// <summary>
/// Covers the Y-DNA clade integration on qpAdm orders: order creation still succeeds with the new
/// background-compute enqueue, and the cached <see cref="QpadmCladeResult"/> surfaces on the order's
/// qpadm-result response (the data behind the "Y-DNA Haplogroup" tab).
/// </summary>
public class QpadmOrderCladeEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task CreateQpadmOrder_StillSucceeds_WhenCladeComputeIsEnqueued()
    {
        // CreateOrderViaApiAsync posts a qpAdm order; success proves the added Hangfire enqueue does not
        // break order creation (enqueue failures are swallowed by design).
        var created = await CreateOrderViaApiAsync(Client, Factory.Services);

        Assert.Equal("qpAdm", created.Service);
        Assert.Equal("Pending", created.Status);
        Assert.True(created.GeneticInspectionId > 0);
    }

    [Fact]
    public async Task GetQpadmResult_WithCachedCompletedClade_ReturnsCladePayload()
    {
        var (orderId, inspectionId) = await SeedCompletedQpadmOrderWithResultAsync();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.QpadmCladeResults.Add(new QpadmCladeResult
            {
                GeneticInspectionId = inspectionId,
                Status = CladeAnalysisStatus.Completed,
                ResultsVersion = "v1",
                Clade = "J-Z1865",
                Score = 12.0,
                NextPredictionClade = "J-P58",
                NextPredictionScore = 11.0,
                Lineage = ["J", "J-P58", "J-Z1865"],
                Downstream = [new CladeDownstreamItem { Clade = "J-Y4000", Children = 3 }],
                PositivesUsed = 40,
                NegativesUsed = 8,
                YReads = 1200,
                SourceFormat = "microarray",
                EffectiveBuild = "hg19",
                CreatedBy = "test-seed",
            });
            await db.SaveChangesAsync();
        }

        var response = await Client.GetAsync($"/api/orders/{orderId}/qpadm-result");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<GetOrderQpadmResultContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.NotNull(body!.YDna);
        Assert.Equal("Completed", body.YDna!.Status);
        Assert.NotNull(body.YDna.Clade);
        Assert.Equal("J-Z1865", body.YDna.Clade!.Clade);
        Assert.Equal(["J", "J-P58", "J-Z1865"], body.YDna.Clade.Lineage);
        Assert.Equal("hg19", body.YDna.Clade.EffectiveBuild);
        Assert.NotNull(body.YDna.Clade.NextPrediction);
        Assert.Equal("J-P58", body.YDna.Clade.NextPrediction!.Clade);
    }

    [Fact]
    public async Task GetQpadmResult_WithCachedNoYData_ReturnsMessageAndNoClade()
    {
        var (orderId, inspectionId) = await SeedCompletedQpadmOrderWithResultAsync();

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.QpadmCladeResults.Add(new QpadmCladeResult
            {
                GeneticInspectionId = inspectionId,
                Status = CladeAnalysisStatus.NoYData,
                ResultsVersion = "v1",
                Message = "No Y-chromosome markers were found in the uploaded file.",
                CreatedBy = "test-seed",
            });
            await db.SaveChangesAsync();
        }

        var response = await Client.GetAsync($"/api/orders/{orderId}/qpadm-result");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<GetOrderQpadmResultContract.Response>(JsonOptions);
        Assert.NotNull(body);
        Assert.NotNull(body!.YDna);
        Assert.Equal("NoYData", body.YDna!.Status);
        Assert.Null(body.YDna.Clade);
        Assert.False(string.IsNullOrEmpty(body.YDna.Message));
    }

    /// <summary>
    /// Creates a qpAdm order via the API, seeds a minimal qpAdm result (so the result endpoint returns 200),
    /// and marks the order Completed. Returns the order id and outputs the genetic inspection id.
    /// </summary>
    private async Task<(int OrderId, int InspectionId)> SeedCompletedQpadmOrderWithResultAsync()
    {
        // Reference catalog provides eras + populations (with music tracks) the result projection needs.
        await using (var seedScope = Factory.Services.CreateAsyncScope())
        {
            var seeder = seedScope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedReferenceCatalogAsync();
        }

        var created = await CreateOrderViaApiAsync(Client, Factory.Services);
        var inspectionId = created.GeneticInspectionId;

        await using (var scope = Factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var era = await db.QpadmEras.Include(e => e.Populations).FirstAsync();
            var pops = era.Populations.Take(2).ToList();

            var qpadmResult = new QpadmResult
            {
                GeneticInspectionId = inspectionId,
                ResultsVersion = "v1",
                CreatedBy = "test-seed",
            };
            db.QpadmResults.Add(qpadmResult);
            await db.SaveChangesAsync();

            var eraGroup = new QpadmResultEraGroup
            {
                QpadmResultId = qpadmResult.Id,
                EraId = era.Id,
                PValue = 0.05m,
                RightSources = "WHG, EHG",
            };
            db.Set<QpadmResultEraGroup>().Add(eraGroup);
            await db.SaveChangesAsync();

            db.Set<QpadmResultPopulation>().Add(new QpadmResultPopulation
            {
                QpadmResultEraGroupId = eraGroup.Id,
                PopulationId = pops[0].Id,
                Percentage = 60m,
                StandardError = 1.2m,
                ZScore = 2.5m,
            });
            db.Set<QpadmResultPopulation>().Add(new QpadmResultPopulation
            {
                QpadmResultEraGroupId = eraGroup.Id,
                PopulationId = pops[1].Id,
                Percentage = 40m,
                StandardError = 0.8m,
                ZScore = 1.9m,
            });
            await db.SaveChangesAsync();
        }

        await SetOrderStatusAsync(Factory.Services, created.Id, OrderStatus.Completed);

        return (created.Id, inspectionId);
    }
}
