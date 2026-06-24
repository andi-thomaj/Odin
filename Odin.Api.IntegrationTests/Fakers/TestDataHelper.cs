using System.Net.Http.Headers;
using System.Net.Http.Json;
using Bogus;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.OrderManagement.Models;
using Odin.Api.Endpoints.UserManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Fakers;

public static class TestDataHelper
{
    private static readonly Faker Faker = new();

    public static HttpClient CreateClientWithRole(
        CustomWebApplicationFactory factory,
        string identityId,
        string role)
    {
        // Must use CreateDefaultClient with ApiVersionPrefixHandler so /api/... is rewritten
        // to /v1/api/... — otherwise requests hit no route in the /v1 group and 404 instead
        // of exercising the authorization policy. IntegrationTestBase.Client does the same.
        var client = factory.CreateDefaultClient(new ApiVersionPrefixHandler());
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-Identity-Id", identityId);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-App-Role", role);
        return client;
    }

    public static HttpClient CreateUnauthenticatedClient(CustomWebApplicationFactory factory)
    {
        var client = factory.CreateDefaultClient(new ApiVersionPrefixHandler());
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-Unauthenticated", "true");
        return client;
    }

    public static async Task<CreateUserContract.Response> CreateUserAsync(
        HttpClient client,
        string? identityId = null,
        string? firstName = null,
        string? lastName = null,
        string? email = null)
    {
        var request = UserFaker.GenerateCreateRequest();
        if (identityId is not null) request.IdentityId = identityId;
        if (firstName is not null) request.FirstName = firstName;
        if (lastName is not null) request.LastName = lastName;
        if (email is not null) request.Email = email;

        var response = await client.PostAsJsonAsync("/api/users", request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateUserContract.Response>())!;
    }

    public static async Task<(List<int> RegionIds, List<QpadmEthnicity> Ethnicities)> SeedEthnicitiesAndRegionsAsync(
        IServiceProvider services,
        int ethnicityCount = 1,
        int regionsPerEthnicity = 2)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var ethnicities = new List<QpadmEthnicity>();
        var regionIds = new List<int>();

        for (var i = 0; i < ethnicityCount; i++)
        {
            var eth = new QpadmEthnicity { Name = Faker.Address.Country() + $"_{Guid.NewGuid():N}"[..8] };
            db.QpadmEthnicities.Add(eth);
            await db.SaveChangesAsync();
            ethnicities.Add(eth);

            for (var j = 0; j < regionsPerEthnicity; j++)
            {
                var region = new QpadmRegion
                {
                    Name = Faker.Address.State() + $"_{Guid.NewGuid():N}"[..8],
                    Ethnicity = eth
                };
                db.QpadmRegions.Add(region);
                await db.SaveChangesAsync();
                regionIds.Add(region.Id);
            }
        }

        return (regionIds, ethnicities);
    }

    public static async Task<int> SeedRawGeneticFileAsync(IServiceProvider services, string? fileName = null, string? createdBy = null)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var file = new RawGeneticFile
        {
            RawDataFileName = fileName ?? "test_data.txt",
            RawData = "rsid,chromosome,position,genotype\nrs1,1,1,AA\n"u8.ToArray(),
            CreatedBy = createdBy ?? "test-seed"
        };
        db.RawGeneticFiles.Add(file);
        await db.SaveChangesAsync();
        return file.Id;
    }

    public static async Task<CreateOrderContract.Response> CreateOrderViaApiAsync(
        HttpClient client,
        IServiceProvider services,
        int? existingFileId = null)
    {
        var (regionIds, _) = await SeedEthnicitiesAndRegionsAsync(services, 1, 1);

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(Faker.Name.FirstName()), "FirstName");
        content.Add(new StringContent(Faker.Name.LastName()), "LastName");
        content.Add(new StringContent("Male"), "Gender");
        content.Add(new StringContent("0"), "Service");
        // Orders are now paid (iOS IAP). The /orders/purchase endpoint validates this StoreKit transaction;
        // under the Testing environment the signature check is skipped, so an unsigned-but-well-formed JWS
        // with the right bundle/product is accepted. A fresh transaction id per call keeps the unique
        // (App, TransactionId) index from collapsing multiple orders into one.
        content.Add(new StringContent(BuildAppStoreTransactionJws(ServiceType.qpAdm)), "AppStoreTransaction");

        foreach (var regionId in regionIds)
            content.Add(new StringContent(regionId.ToString()), "RegionIds");

        if (existingFileId.HasValue)
        {
            content.Add(new StringContent(existingFileId.Value.ToString()), "ExistingFileId");
        }
        else
        {
            var fileBytes = "rsid,chromosome,position,genotype\nrs1,1,1,AA\n"u8.ToArray();
            var filePart = new ByteArrayContent(fileBytes);
            filePart.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
            content.Add(filePart, "File", "kit.csv");
        }

        var response = await client.PostAsync("/api/orders/purchase", content);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<CreateOrderContract.Response>(JsonOptions))!;
    }

    /// <summary>
    /// Builds a well-formed (but UNSIGNED) StoreKit 2 transaction JWS for tests. The Testing environment
    /// disables signature verification, so only the decoded payload (bundle id + product id → service) is
    /// checked. Defaults to the qpAdm/G25 product ids that match <c>AppleIapOptions</c>' defaults.
    /// </summary>
    public static string BuildAppStoreTransactionJws(
        ServiceType service,
        string? transactionId = null,
        string? bundleId = null,
        string? productId = null,
        string environment = "Xcode")
    {
        transactionId ??= Guid.NewGuid().ToString("N");
        bundleId ??= "io.ancestrify.app";
        productId ??= service == ServiceType.g25 ? "io.ancestrify.app.g25" : "io.ancestrify.app.qpadm";

        var header = Base64UrlEncode("{\"alg\":\"ES256\",\"x5c\":[]}"u8.ToArray());
        var payloadJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            bundleId,
            productId,
            transactionId,
            originalTransactionId = transactionId,
            purchaseDate = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            type = "Consumable",
            environment,
        });
        var payload = Base64UrlEncode(System.Text.Encoding.UTF8.GetBytes(payloadJson));
        var signature = Base64UrlEncode("test-signature"u8.ToArray());
        return $"{header}.{payload}.{signature}";
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static async Task SetOrderStatusAsync(
        IServiceProvider services,
        int orderId,
        OrderStatus status)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var order = await db.QpadmOrders.FirstAsync(o => o.Id == orderId);
        order.Status = status;
        await db.SaveChangesAsync();
    }

    public static async Task SeedNotificationsAsync(
        IServiceProvider services,
        int recipientUserId,
        int count = 3)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        for (var i = 0; i < count; i++)
        {
            db.Notifications.Add(new Notification
            {
                RecipientUserId = recipientUserId,
                Type = NotificationType.OrderCompleted,
                Title = $"Test Notification {i + 1}",
                Message = Faker.Lorem.Sentence(),
                CreatedBy = "test-seed",
                CreatedAt = DateTime.UtcNow.AddMinutes(-count + i),
                UpdatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
    }

    public static async Task<int> ResolveUserIdAsync(IServiceProvider services, string identityId)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.AsNoTracking().FirstAsync(u => u.IdentityId == identityId);
        return user.Id;
    }

    public static readonly System.Text.Json.JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
