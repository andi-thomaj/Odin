using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Data.Enums;
using Odin.Api.Endpoints.UserManagement.Models;
using Respawn;
using Respawn.Graph;

namespace Odin.Api.IntegrationTests.Infrastructure;

[Collection(nameof(IntegrationTestCollection))]
public abstract class IntegrationTestBase : IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly HttpClient Client;
    private Respawner? _respawner;

    protected IntegrationTestBase(CustomWebApplicationFactory factory)
    {
        Factory = factory;
        Client = factory.CreateDefaultClient(new ApiVersionPrefixHandler());
    }

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(Factory.ConnectionString);
        await connection.OpenAsync();

        _respawner = await Respawner.CreateAsync(connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                // Do not clear migration history: next host startup would re-apply migrations while tables still exist (42P07).
                TablesToIgnore = [new Table("public", "__EFMigrationsHistory")],
                // Reset identity sequences after delete so re-seeded reference data lands on the
                // same IDs the JSON seed files hardcode (e.g. g25_distance_population_samples
                // references G25DistanceEraId 1–6; without reseed, post-Respawn eras land on 7–12).
                WithReseed = true,
            });

        await _respawner.ResetAsync(connection);

        Client.DefaultRequestHeaders.Remove("X-Test-Identity-Id");
        Client.DefaultRequestHeaders.Remove("X-Test-App-Role");
        Client.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-Identity-Id", "auth0|integration-default");
        Client.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-App-Role", "Admin");

        var seedRequest = new CreateUserContract.Request
        {
            IdentityId = "auth0|integration-default",
            FirstName = "Integration",
            LastName = "User",
            Email = "integration-default@test.local"
        };
        var seedResponse = await Client.PostAsJsonAsync("/api/users", seedRequest);
        if (!seedResponse.IsSuccessStatusCode)
        {
            var body = await seedResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Seed POST /api/users failed: {(int)seedResponse.StatusCode}. Body: {body}");
        }

        // The user-create endpoint always assigns AppRole.User regardless of the auth context, so the
        // DB record is User even though the test client authenticates as Admin (X-Test-App-Role).
        // Several product paths (e.g. OrderService.CreateG25OrderAsync's "admin can skip payment"
        // gate) read the role from the DB rather than the auth claim, so the integration default
        // user must actually be Admin in the database for those tests to exercise admin behaviour.
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var dbUser = await db.Users.SingleAsync(u => u.IdentityId == "auth0|integration-default");
        dbUser.Role = AppRole.Admin;
        await db.SaveChangesAsync();

        await SeedPaddleCatalogStubAsync(db);
    }

    // ── Paddle catalog stub ─────────────────────────────────────────────────────────────
    // TODO(paddle-removal): delete this method, the Data.Enums + PaddleProduct/PaddlePrice
    // imports it adds, and the call above as soon as the Paddle integration is removed.
    //
    // Why this exists: OrderPricingService.ComputeAsync requires an active PaddleProduct
    // (Kind="service") with at least one active PaddlePrice for every ServiceType the
    // tests create orders against. Production syncs this catalog from Paddle's API; CI
    // has no real Paddle and Respawn wipes the table between tests. Without these stubs,
    // every order-creation test 400s with "No active Paddle product is linked to the
    // selected service." Two minimal rows per ServiceType are enough to pass the gate;
    // pricing math falls out of the catalog values below but no test asserts on the totals.
    private static async Task SeedPaddleCatalogStubAsync(ApplicationDbContext db)
    {
        if (await db.PaddleProducts.AnyAsync()) return;

        var now = DateTime.UtcNow;

        var stubs = new (ServiceType Service, string ProductId, string PriceId, string Amount)[]
        {
            (ServiceType.qpAdm, "pro_test_qpadm", "pri_test_qpadm", "2900"),
            (ServiceType.g25, "pro_test_g25", "pri_test_g25", "1900"),
        };

        foreach (var (service, productId, priceId, amount) in stubs)
        {
            db.PaddleProducts.Add(new PaddleProduct
            {
                PaddleProductId = productId,
                Name = $"Test {service} Service",
                Status = "active",
                Kind = "service",
                ServiceType = service,
                LastSyncedAt = now,
                Prices =
                [
                    new PaddlePrice
                    {
                        PaddlePriceId = priceId,
                        PaddleProductId = productId,
                        UnitPriceAmount = amount,
                        UnitPriceCurrency = "USD",
                        Status = "active",
                        LastSyncedAt = now,
                    },
                ],
            });
        }

        await db.SaveChangesAsync();
    }

    public Task DisposeAsync()
    {
        Client.Dispose();
        return Task.CompletedTask;
    }

    protected async Task<ApplicationDbContext> GetDbContextAsync()
    {
        var scope = Factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    }

    protected IServiceScope CreateScope() => Factory.Services.CreateScope();
}
