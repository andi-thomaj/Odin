using System.Net;
using System.Net.Http.Json;
using Odin.Api.Endpoints.UserManagement.Models;
using Odin.Api.IntegrationTests.Fakers;
using Odin.Api.IntegrationTests.Infrastructure;
using static Odin.Api.IntegrationTests.Fakers.TestDataHelper;

namespace Odin.Api.IntegrationTests.Endpoints.UserManagement;

public class UserEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // ── GET /api/users (list) ──────────────────────────────────────

    [Fact]
    public async Task ListUsers_AsAdmin_ReturnsPagedUsers()
    {
        var response = await Client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ListUsersContract.Response>();
        Assert.NotNull(body);
        Assert.True(body!.TotalCount >= 1);
        Assert.NotEmpty(body.Items);
        Assert.Contains(body.Items, u => u.IdentityId == "auth0|integration-default");
    }

    [Fact]
    public async Task ListUsers_AsNonAdmin_ReturnsForbidden()
    {
        using var userClient = CreateClientWithRole(Factory, "auth0|integration-default", "User");

        var response = await userClient.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task ListUsers_Pagination_RespectsSkipAndTake()
    {
        for (var i = 0; i < 3; i++)
            await CreateUserAsync(Client);

        var response = await Client.GetAsync("/api/users?skip=0&take=2");
        var body = await response.Content.ReadFromJsonAsync<ListUsersContract.Response>();

        Assert.NotNull(body);
        Assert.Equal(2, body!.Items.Count);
        Assert.True(body.TotalCount >= 4); // 1 seed + 3 created
    }

    [Fact]
    public async Task ListUsers_TakeClampedToMax100()
    {
        var response = await Client.GetAsync("/api/users?take=999");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ListUsersContract.Response>();
        Assert.NotNull(body);
        Assert.True(body!.Take <= 100);
    }

    // ── POST /api/users (create) ───────────────────────────────────

    [Fact]
    public async Task CreateUser_WithValidRequest_ReturnsOk()
    {
        var request = UserFaker.GenerateCreateRequest();

        var response = await Client.PostAsJsonAsync("/api/users", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateUserContract.Response>();
        Assert.NotNull(result);
        Assert.Equal(request.FirstName, result!.FirstName);
        Assert.Equal(request.LastName, result.LastName);
        Assert.Equal(request.Email, result.Email);
        Assert.True(result.Id > 0);
        Assert.True(result.IsNewUser);
    }

    [Fact]
    public async Task CreateUser_WithExistingIdentityId_ReturnsExistingUser()
    {
        var request = UserFaker.GenerateCreateRequest();
        await Client.PostAsJsonAsync("/api/users", request);

        var response = await Client.PostAsJsonAsync("/api/users", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateUserContract.Response>();
        Assert.NotNull(result);
        Assert.False(result!.IsNewUser);
    }

    [Fact]
    public async Task CreateUser_WithInvalidEmail_ReturnsBadRequest()
    {
        var request = UserFaker.GenerateCreateRequest(r => r.Email = "invalid-email");

        var response = await Client.PostAsJsonAsync("/api/users", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithMissingFirstName_ReturnsBadRequest()
    {
        var request = UserFaker.GenerateCreateRequest(r => r.FirstName = null);

        var response = await Client.PostAsJsonAsync("/api/users", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithAllOptionalFields_ReturnsOk()
    {
        var request = UserFaker.GenerateCreateRequest(r => r.Username = "custom_username");

        var response = await Client.PostAsJsonAsync("/api/users", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateUserContract.Response>();
        Assert.NotNull(result);
        Assert.True(result!.IsNewUser);
    }

    // ── GET /api/users/{identityId} ────────────────────────────────

    [Fact]
    public async Task GetUserByIdentityId_WhenExists_ReturnsUser()
    {
        var created = await CreateUserAsync(Client);

        var response = await Client.GetAsync($"/api/users/{created.IdentityId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<GetUserContract.Response>();
        Assert.NotNull(user);
        Assert.Equal(created.IdentityId, user!.IdentityId);
    }

    [Fact]
    public async Task GetUserByIdentityId_WhenNotExists_ReturnsNotFound()
    {
        var response = await Client.GetAsync("/api/users/auth0|nonexistent-user");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── PUT /api/users/{identityId} ────────────────────────────────

    [Fact]
    public async Task UpdateUser_ValidRequest_ReturnsUpdatedUser()
    {
        var created = await CreateUserAsync(Client);
        var updateRequest = UserFaker.GenerateUpdateRequest();

        var response = await Client.PutAsJsonAsync($"/api/users/{created.IdentityId}", updateRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = await response.Content.ReadFromJsonAsync<UpdateUserContract.Response>();
        Assert.NotNull(updated);
        Assert.Equal(updateRequest.FirstName, updated!.FirstName);
        Assert.Equal(updateRequest.LastName, updated.LastName);
        Assert.Equal(updateRequest.Username, updated.Username);
    }

    [Fact]
    public async Task UpdateUser_NonExistentUser_ReturnsNotFound()
    {
        var updateRequest = UserFaker.GenerateUpdateRequest();

        var response = await Client.PutAsJsonAsync("/api/users/auth0|nonexistent", updateRequest);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UpdateUser_MissingFirstName_ReturnsBadRequest()
    {
        var created = await CreateUserAsync(Client);
        var request = new UpdateUserContract.Request
        {
            FirstName = "",
            LastName = "Valid",
            Username = "valid_user"
        };

        var response = await Client.PutAsJsonAsync($"/api/users/{created.IdentityId}", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ── DELETE /api/users/{identityId} ─────────────────────────────

    [Fact]
    public async Task DeleteUser_AsAdmin_ReturnsNoContent()
    {
        var created = await CreateUserAsync(Client);

        var response = await Client.DeleteAsync($"/api/users/{created.IdentityId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var getResponse = await Client.GetAsync($"/api/users/{created.IdentityId}");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_AsNonAdmin_ReturnsForbidden()
    {
        var created = await CreateUserAsync(Client);

        using var userClient = CreateClientWithRole(Factory, "auth0|integration-default", "User");
        var response = await userClient.DeleteAsync($"/api/users/{created.IdentityId}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task DeleteUser_NonExistentUser_ReturnsNotFound()
    {
        var response = await Client.DeleteAsync("/api/users/auth0|nonexistent");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // ── PATCH /api/users/{identityId}/role ─────────────────────────

    [Fact]
    public async Task UpdateRole_ValidRole_ReturnsUpdatedRole()
    {
        var created = await CreateUserAsync(Client);

        var response = await Client.PatchAsJsonAsync(
            $"/api/users/{created.IdentityId}/role",
            new UpdateUserRoleContract.Request { Role = "Scientist" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UpdateUserRoleContract.Response>();
        Assert.NotNull(result);
        Assert.Equal("Scientist", result!.Role);
    }

    [Fact]
    public async Task UpdateRole_InvalidRole_ReturnsBadRequest()
    {
        var created = await CreateUserAsync(Client);

        var response = await Client.PatchAsJsonAsync(
            $"/api/users/{created.IdentityId}/role",
            new UpdateUserRoleContract.Request { Role = "SuperAdmin" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_AsNonAdmin_ReturnsForbidden()
    {
        var created = await CreateUserAsync(Client);

        using var userClient = CreateClientWithRole(Factory, "auth0|integration-default", "User");
        var response = await userClient.PatchAsJsonAsync(
            $"/api/users/{created.IdentityId}/role",
            new UpdateUserRoleContract.Request { Role = "Admin" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRole_NonExistentUser_ReturnsNotFound()
    {
        var response = await Client.PatchAsJsonAsync(
            "/api/users/auth0|nonexistent/role",
            new UpdateUserRoleContract.Request { Role = "Admin" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
