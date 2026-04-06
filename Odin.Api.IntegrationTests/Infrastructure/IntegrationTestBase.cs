using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Odin.Api.Data;
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
                TablesToIgnore = [new Table("public", "__EFMigrationsHistory")]
            });

        await _respawner.ResetAsync(connection);

        await using (var seedScope = Factory.Services.CreateAsyncScope())
        {
            var seeder = seedScope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
            await seeder.SeedCatalogCommerceAsync();
        }

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
