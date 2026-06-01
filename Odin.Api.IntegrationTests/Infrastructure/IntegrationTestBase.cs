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
    private const string DefaultIdentityId = "auth0|integration-default";

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

        SetTestHeaders(Client, DefaultIdentityId, AppRole.Admin);
        await SeedUserAsync(Client, DefaultIdentityId, AppRole.Admin, "Integration", "User",
            "integration-default@test.local");
    }

    /// <summary>
    /// Returns a fresh <see cref="HttpClient"/> scoped to the given test identity + role, with
    /// the matching <c>application_users</c> row already JIT-provisioned + role-promoted. Use
    /// this when a single test needs to act as more than one user (e.g. owner vs. admin paths,
    /// authorization boundaries) — each call returns an independent client, so headers don't
    /// cross-contaminate. The shared <see cref="Client"/> is the default-Admin convenience
    /// handle and continues to work unchanged.
    /// </summary>
    protected async Task<HttpClient> CreateClientAsAsync(
        string identityId,
        AppRole role = AppRole.User,
        string? email = null,
        string firstName = "Test",
        string lastName = "User")
    {
        var client = Factory.CreateDefaultClient(new ApiVersionPrefixHandler());
        SetTestHeaders(client, identityId, role);

        // Mirror the auth handler's case-handling — identity IDs flow into headers verbatim, but
        // the email needs to be a deterministic, syntactically valid local-part.
        var safeLocal = new string(identityId.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '.').ToArray());
        if (string.IsNullOrEmpty(safeLocal)) safeLocal = "user";
        await SeedUserAsync(client, identityId, role, firstName, lastName,
            email ?? $"{safeLocal.ToLowerInvariant()}@test.local");

        return client;
    }

    private static void SetTestHeaders(HttpClient client, string identityId, AppRole role)
    {
        client.DefaultRequestHeaders.Remove("X-Test-Identity-Id");
        client.DefaultRequestHeaders.Remove("X-Test-App-Role");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-Identity-Id", identityId);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-App-Role", role.ToString());
    }

    private async Task SeedUserAsync(
        HttpClient client,
        string identityId,
        AppRole role,
        string firstName,
        string lastName,
        string email)
    {
        var seedRequest = new CreateUserContract.Request
        {
            IdentityId = identityId,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
        };
        var seedResponse = await client.PostAsJsonAsync("/api/users", seedRequest);
        if (!seedResponse.IsSuccessStatusCode)
        {
            var body = await seedResponse.Content.ReadAsStringAsync();
            throw new InvalidOperationException(
                $"Seed POST /api/users for {identityId} failed: {(int)seedResponse.StatusCode}. Body: {body}");
        }

        // The user-create endpoint always assigns AppRole.User regardless of the auth context;
        // promote in the DB when the test wanted a privileged role.
        if (role != AppRole.User)
        {
            await using var scope = Factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var dbUser = await db.Users.SingleAsync(u => u.IdentityId == identityId);
            dbUser.Role = role;
            await db.SaveChangesAsync();
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
