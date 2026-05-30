namespace Odin.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Test-only handler that rewrites <c>/api/...</c> requests to <c>/v1/api/...</c> so the
/// suite's existing test paths continue to work after Program.cs introduced the
/// <c>/v1</c> route group. Hub and health-check paths are passed through unchanged.
/// </summary>
internal sealed class ApiVersionPrefixHandler : DelegatingHandler
{
    private const string ApiPrefix = "/api/";
    private const string VersionedPrefix = "/v1/api/";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        if (request.RequestUri is { } uri && uri.AbsolutePath.StartsWith(ApiPrefix, StringComparison.Ordinal))
        {
            var newPath = VersionedPrefix + uri.AbsolutePath[ApiPrefix.Length..];
            request.RequestUri = new UriBuilder(uri) { Path = newPath }.Uri;
        }

        return base.SendAsync(request, cancellationToken);
    }
}
