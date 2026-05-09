using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
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
        Client = factory.CreateClient();
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
