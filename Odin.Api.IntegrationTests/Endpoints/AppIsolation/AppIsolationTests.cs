using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Data.Entities;
using Odin.Api.Endpoints.UserManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;
using Odin.Api.Middleware;

namespace Odin.Api.IntegrationTests.Endpoints.AppIsolation;

/// <summary>
/// Multi-app data isolation. The same Auth0 sub is a SEPARATE account per application (resolved from the
/// <c>X-App</c> header), and an unknown app is rejected. Role enrichment is bypassed under the Testing
/// environment, so these exercise <see cref="AppResolutionMiddleware"/> + the per-app EF query filters +
/// app-scoped provisioning (<c>POST /api/users</c>).
/// </summary>
public class AppIsolationTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    private async Task SeedApplicationsAsync()
    {
        // Seeds the applications registry (among other reference data) so X-App validation has rows to match
        // after the per-test Respawn wipe.
        await using var scope = Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().SeedReferenceCatalogAsync();
    }

    private HttpClient CreateAppClient(string identityId, string app, AppRole role = AppRole.User)
    {
        var client = Factory.CreateDefaultClient(new ApiVersionPrefixHandler());
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-Identity-Id", identityId);
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-App-Role", role.ToString());
        client.DefaultRequestHeaders.TryAddWithoutValidation(AppResolutionMiddleware.HeaderName, app);
        return client;
    }

    [Fact]
    public async Task SameIdentityInTwoApps_GetsTwoSeparateAccounts()
    {
        await SeedApplicationsAsync();
        const string sharedSub = "auth0|app-isolation-shared";

        var ancestrify = CreateAppClient(sharedSub, "ancestrify");
        var aurora = CreateAppClient(sharedSub, "aurora");

        var request = new CreateUserContract.Request
        {
            IdentityId = sharedSub,
            FirstName = "Shared",
            LastName = "User",
            Email = "shared@test.local",
        };

        var ancestrifyResponse = await ancestrify.PostAsJsonAsync("/api/users", request);
        ancestrifyResponse.EnsureSuccessStatusCode();
        var ancestrifyUser = await ancestrifyResponse.Content.ReadFromJsonAsync<CreateUserContract.Response>();

        var auroraResponse = await aurora.PostAsJsonAsync("/api/users", request);
        auroraResponse.EnsureSuccessStatusCode();
        var auroraUser = await auroraResponse.Content.ReadFromJsonAsync<CreateUserContract.Response>();

        // First login to the SECOND app provisions a fresh, independent account — not the first app's row.
        Assert.True(ancestrifyUser!.IsNewUser);
        Assert.True(auroraUser!.IsNewUser);
        Assert.NotEqual(ancestrifyUser.Id, auroraUser.Id);

        // The database holds two distinct application_users rows for the one Auth0 sub, one per app.
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var rows = await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.IdentityId == sharedSub)
            .OrderBy(u => u.App)
            .ToListAsync();

        Assert.Equal(2, rows.Count);
        Assert.Equal("ancestrify", rows[0].App);
        Assert.Equal("aurora", rows[1].App);
    }

    [Fact]
    public async Task UnknownApp_IsRejectedWith400()
    {
        await SeedApplicationsAsync();
        var client = CreateAppClient("auth0|app-isolation-unknown", "does-not-exist");

        // AppResolutionMiddleware short-circuits before routing, so any path returns 400 for an unknown app.
        var response = await client.GetAsync("/api/users/probe");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
