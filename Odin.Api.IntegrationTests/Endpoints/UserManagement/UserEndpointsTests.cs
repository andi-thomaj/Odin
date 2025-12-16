using System.Net;
using System.Net.Http.Json;
using Odin.Api.Endpoints.UserManagement.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.UserManagement;

public class UserEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task GetUsers_ReturnsOk()
    {
        // Act
        var response = await Client.GetAsync("/api/users");

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task CreateUser_WithValidRequest_ReturnsOk()
    {
        // Arrange
        var request = new CreateUserContract.Request
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "john.doe@example.com"
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
    }

    [Fact]
    public async Task CreateUser_WithInvalidEmail_ReturnsBadRequest()
    {
        // Arrange
        var request = new CreateUserContract.Request
        {
            FirstName = "John",
            LastName = "Doe",
            Email = "invalid-email"
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
