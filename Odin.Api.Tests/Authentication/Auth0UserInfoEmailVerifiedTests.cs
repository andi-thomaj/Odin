using System.Net;
using System.Text;
using Odin.Api.Authentication;

namespace Odin.Api.Tests.Authentication;

public class Auth0UserInfoEmailVerifiedTests
{
    [Fact]
    public async Task Returns_true_when_userinfo_json_has_email_verified_true()
    {
        var handler = new StubHandler("""{"sub":"x","email_verified":true}""");
        HttpClient client = new(handler) { BaseAddress = new Uri("https://example.com/") };
        var result = await Auth0UserInfoEmailVerified.GetAppEmailVerifiedAsync(
            client,
            "https://tenant.us.auth0.com",
            "token",
            CancellationToken.None);
        Assert.Equal("true", result);
    }

    [Fact]
    public async Task Returns_false_when_userinfo_json_has_email_verified_false()
    {
        var handler = new StubHandler("""{"sub":"x","email_verified":false}""");
        HttpClient client = new(handler) { BaseAddress = new Uri("https://example.com/") };
        var result = await Auth0UserInfoEmailVerified.GetAppEmailVerifiedAsync(
            client,
            "https://tenant.us.auth0.com",
            "token",
            CancellationToken.None);
        Assert.Equal("false", result);
    }

    [Fact]
    public async Task Returns_false_when_http_error()
    {
        var handler = new StubHandler(null, HttpStatusCode.Unauthorized);
        HttpClient client = new(handler) { BaseAddress = new Uri("https://example.com/") };
        var result = await Auth0UserInfoEmailVerified.GetAppEmailVerifiedAsync(
            client,
            "https://tenant.us.auth0.com",
            "token",
            CancellationToken.None);
        Assert.Equal("false", result);
    }

    [Fact]
    public async Task WithStatusAsync_returns_status_code()
    {
        var handler = new StubHandler(null, HttpStatusCode.Unauthorized);
        HttpClient client = new(handler) { BaseAddress = new Uri("https://example.com/") };
        var (value, status) = await Auth0UserInfoEmailVerified.GetAppEmailVerifiedWithStatusAsync(
            client,
            "https://tenant.us.auth0.com",
            "token",
            CancellationToken.None);
        Assert.Equal("false", value);
        Assert.Equal(401, status);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string? _json;
        private readonly HttpStatusCode _status;

        public StubHandler(string? json, HttpStatusCode status = HttpStatusCode.OK)
        {
            _json = json;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (_status != HttpStatusCode.OK)
                return Task.FromResult(new HttpResponseMessage(_status));

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_json ?? "{}", Encoding.UTF8, "application/json"),
            });
        }
    }
}
