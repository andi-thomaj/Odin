using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Endpoints.OrderManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;
using static Odin.Api.IntegrationTests.Fakers.TestDataHelper;

namespace Odin.Api.IntegrationTests.Endpoints.OrderManagement;

/// <summary>
/// Order-creation edge cases on the qpAdm upload path. The unique filtered index
/// (CreatedBy, RawDataFileName WHERE IsDeleted = false) on raw_genetic_files means a user cannot
/// have two active uploads with the same file name. Order creation stamps each upload's stored name
/// with the user + a UTC timestamp, so re-uploading a same-named file across orders never collides.
/// </summary>
public class OrderCreationEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task CreateQpadmOrder_WithDuplicateFileName_BothSucceed_WithDistinctStoredNames()
    {
        // Same user uploads "genome.txt" twice across two orders. Both must succeed — the stored name
        // is made unique per upload, so the (CreatedBy, RawDataFileName) index never trips.
        var first = await PostQpadmOrderWithFileNameAsync("genome.txt");
        var second = await PostQpadmOrderWithFileNameAsync("genome.txt");

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);

        var firstId = (await first.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions))!.GeneticInspectionId;
        var secondId = (await second.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions))!.GeneticInspectionId;

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var storedNames = await db.QpadmGeneticInspections
            .Where(gi => gi.Id == firstId || gi.Id == secondId)
            .Select(gi => gi.RawGeneticFile.RawDataFileName)
            .ToListAsync();

        Assert.Equal(2, storedNames.Count);
        Assert.Equal(2, storedNames.Distinct().Count());
        // Original stem + extension preserved, with the order subject's name + timestamp in between.
        Assert.All(storedNames, n => Assert.StartsWith("genome-", n));
        Assert.All(storedNames, n => Assert.EndsWith(".txt", n));
        Assert.All(storedNames, n => Assert.Contains("AdaLovelace", n));
    }

    private async Task<HttpResponseMessage> PostQpadmOrderWithFileNameAsync(string fileName)
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(Factory.Services, 1, 1);

        using var content = new MultipartFormDataContent
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
        content.Add(filePart, "File", fileName);

        return await Client.PostAsync("/api/orders", content);
    }
}
