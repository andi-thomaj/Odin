using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Odin.Api.Services.Email;

namespace Odin.Api.Tests.Services.Email;

public class ResendAudienceServiceTests
{
    [Fact]
    public async Task AddContactAsync_PostsJsonWithSegment_WhenAudienceIdConfigured()
    {
        HttpRequestMessage? captured = null;
        string? bodyJson = null;
        var handler = new StubHandler(async (req, _) =>
        {
            captured = req;
            bodyJson = req.Content is not null ? await req.Content.ReadAsStringAsync() : null;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = "c1", @object = "contact" }),
            };
        });
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var options = Options.Create(new ResendEmailOptions
        {
            ApiKey = "re_test_key",
            AudienceId = "seg_marketing",
        });
        var svc = new ResendAudienceService(client, options, NullLogger<ResendAudienceService>.Instance);

        await svc.AddContactAsync("user@example.com", "Jane", "Doe");

        Assert.NotNull(captured);
        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.EndsWith("contacts", captured.RequestUri?.ToString(), StringComparison.Ordinal);
        Assert.NotNull(bodyJson);
        Assert.Contains("user@example.com", bodyJson!, StringComparison.Ordinal);
        Assert.Contains("seg_marketing", bodyJson!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddContactAsync_DoesNotThrow_OnApiError()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"bad\"}"),
            }));
        using var client = new HttpClient(handler) { BaseAddress = new Uri("https://api.resend.com/") };
        var options = Options.Create(new ResendEmailOptions { ApiKey = "re_test", AudienceId = "s1" });
        var svc = new ResendAudienceService(client, options, NullLogger<ResendAudienceService>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.AddContactAsync("x@y.com", null, null));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) =>
            _send = send;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _send(request, cancellationToken);
    }
}
