using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Odin.Api.Data;
using Odin.Api.Endpoints.AuthRegistration;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.AuthRegistration;

[Trait("Category", "RequiresDocker")]
public class AuthRegistrationEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    [Fact]
    public async Task Register_ValidRequest_ReturnsOk_AndPersistsUser()
    {
        var anonymous = Factory.CreateClient();
        var req = new RegisterContract.Request
        {
            Username = "reguser1",
            FirstName = "John",
            MiddleName = "Quincy",
            LastName = "Public",
            Email = "john.register.unique@test.local",
            Password = "Password1",
        };

        var response = await anonymous.PostAsJsonAsync("/api/auth/register", req);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Email == req.Email);
        Assert.NotNull(user);
        Assert.Equal("John", user!.FirstName);
        Assert.Equal("Quincy", user.MiddleName);
        Assert.Equal("Public", user.LastName);
        Assert.Equal("reguser1", user.Username);
        Assert.StartsWith("auth0|test-reg-", user.IdentityId, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Register_DuplicateEmail_ReturnsValidationError()
    {
        var anonymous = Factory.CreateClient();
        var req = new RegisterContract.Request
        {
            Username = "reguser2",
            FirstName = "Jane",
            LastName = "Doe",
            Email = "duplicate.email@test.local",
            Password = "Password1",
        };

        var first = await anonymous.PostAsJsonAsync("/api/auth/register", req);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var secondReq = new RegisterContract.Request
        {
            Username = "reguser3",
            FirstName = "Jane",
            LastName = "Other",
            Email = req.Email,
            Password = "Password1",
        };
        var second = await anonymous.PostAsJsonAsync("/api/auth/register", secondReq);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Register_DuplicateUsername_ReturnsValidationError()
    {
        var anonymous = Factory.CreateClient();
        var req = new RegisterContract.Request
        {
            Username = "sameusername",
            FirstName = "Ann",
            LastName = "One",
            Email = "user.one.dup@test.local",
            Password = "Password1",
        };

        Assert.Equal(HttpStatusCode.OK, (await anonymous.PostAsJsonAsync("/api/auth/register", req)).StatusCode);

        var secondReq = new RegisterContract.Request
        {
            Username = "sameusername",
            FirstName = "Bob",
            LastName = "Two",
            Email = "user.two.dup@test.local",
            Password = "Password1",
        };
        var second = await anonymous.PostAsJsonAsync("/api/auth/register", secondReq);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task Register_WeakPassword_ReturnsValidationError()
    {
        var anonymous = Factory.CreateClient();
        var req = new RegisterContract.Request
        {
            Username = "weakpass",
            FirstName = "Wil",
            LastName = "Pu",
            Email = "weak.pass@test.local",
            Password = "short",
        };

        var response = await anonymous.PostAsJsonAsync("/api/auth/register", req);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
