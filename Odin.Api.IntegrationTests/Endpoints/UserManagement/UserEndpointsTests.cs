using System.Net;
using System.Net.Http.Json;
using Odin.Api.Endpoints.UserManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.UserManagement;

public class UserEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task ListUsers_AsAdmin_ReturnsPagedUsers()
    {
        // Act (default client is Admin after seed user exists)
        var response = await Client.GetAsync("/api/users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ListUsersContract.Response>();
        Assert.NotNull(body);
        Assert.True(body.TotalCount >= 1);
        Assert.NotEmpty(body.Items);
        Assert.Contains(body.Items, u => u.IdentityId == "auth0|integration-default");
    }

    [Fact]
    public async Task ListUsers_AsNonAdmin_ReturnsForbidden()
    {
        using var userClient = Factory.CreateClient();
        userClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-Identity-Id", "auth0|integration-default");
        userClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-App-Role", "User");

        var response = await userClient.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new CreateUserContract.Request
        {
            IdentityId = "auth0|test-user-123", FirstName = "John", LastName = "Doe", Email = "john.doe@example.com"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/users", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CreateUserContract.Response>();
        Assert.NotNull(result);
        Assert.Equal("John", result.FirstName);
        Assert.Equal("Doe", result.LastName);
        Assert.Equal("john.doe@example.com", result.Email);
        Assert.Equal("auth0|test-user-123", result.IdentityId);
        Assert.True(result.Id > 0);
        Assert.True(result.IsNewUser);
    }

    [Fact]
    public async Task CreateUser_WithExistingIdentityId_ReturnsExistingUser()
    {
        // Arrange
        var request = new CreateUserContract.Request
        {
            IdentityId = "auth0|duplicate-user-456",
            FirstName = "Jane",
            LastName = "Smith",
            Email = "jane.smith@example.com"
        };

        // First call creates the user
        await Client.PostAsJsonAsync("/api/users", request);

        // Act - second call should return existing user
        var response = await Client.PostAsJsonAsync("/api/users", request);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<CreateUserContract.Response>();
        Assert.NotNull(result);
        Assert.False(result.IsNewUser);
        Assert.Equal("auth0|duplicate-user-456", result.IdentityId);
    }

    [Fact]
    public async Task CreateUser_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateUserContract.Request
        {
            IdentityId = "auth0|invalid-email-test", FirstName = "John", LastName = "Doe", Email = "invalid-email"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/users", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithMissingFirstName_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateUserContract.Request
        {
            IdentityId = "auth0|missing-firstname-test",
            FirstName = null,
            LastName = "Doe",
            Email = "john.doe@example.com"
        };

        // Act
        var response = await Client.PostAsJsonAsync("/api/users", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
