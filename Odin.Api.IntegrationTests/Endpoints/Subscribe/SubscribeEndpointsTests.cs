using System.Net;
using System.Net.Http.Json;
using Odin.Api.Endpoints.Subscribe.Models;
using Odin.Api.IntegrationTests.Infrastructure;

namespace Odin.Api.IntegrationTests.Endpoints.Subscribe;

public class SubscribeEndpointsTests(CustomWebApplicationFactory factory) : IntegrationTestBase(factory)
{
    // ── POST /api/public/subscribe ─────────────────────────────────

    [Fact]
    public async Task Join_WithValidEmail_ReturnsOkAndSuccess()
    {
        var response = await Client.PostAsJsonAsync("/api/public/subscribe/",
            new SubscribeRequest("waitlist.signup@example.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<SubscribeResponse>();
        Assert.NotNull(body);
        Assert.True(body!.Success);
    }

    [Fact]
    public async Task Join_IsAnonymous_NoAuthHeadersRequired()
    {
        // A fresh client with no X-Test-* identity headers must still be accepted.
        var anonymous = Factory.CreateDefaultClient(new ApiVersionPrefixHandler());

        var response = await anonymous.PostAsJsonAsync("/api/public/subscribe/",
            new SubscribeRequest("anon.visitor@example.com"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("@nolocal.com")]
    [InlineData("two@@at.com")]
    public async Task Join_WithInvalidEmail_ReturnsBadRequest(string email)
    {
        var response = await Client.PostAsJsonAsync("/api/public/subscribe/",
            new SubscribeRequest(email));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
