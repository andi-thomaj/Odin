using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;

namespace Odin.Api.Services.Paddle.Http;

/// <summary>
/// Adds the <c>Authorization: Bearer</c> and <c>Paddle-Version</c> headers to every Paddle API request.
/// Reads the API key on each call so a config rotation doesn't require an app restart.
/// </summary>
public sealed class PaddleAuthHandler(IOptionsMonitor<PaddleOptions> options) : DelegatingHandler
{
    private const string PaddleVersionHeader = "Paddle-Version";

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var cfg = options.CurrentValue;

        if (request.Headers.Authorization is null && !string.IsNullOrWhiteSpace(cfg.ApiKey))
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", cfg.ApiKey);

        if (!request.Headers.Contains(PaddleVersionHeader) && !string.IsNullOrWhiteSpace(cfg.ApiVersion))
            request.Headers.Add(PaddleVersionHeader, cfg.ApiVersion);

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
