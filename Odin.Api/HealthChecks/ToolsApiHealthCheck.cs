using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;

namespace Odin.Api.HealthChecks
{
    /// <summary>
    /// Reports whether the Python tools API (odin-tools-api) is reachable by pinging its <c>/health</c>.
    /// Registered as a <see cref="HealthStatus.Degraded"/> check: a tools-api outage should be visible
    /// but must NOT mark the whole .NET API unhealthy (most endpoints don't depend on it), so the load
    /// balancer keeps routing. When no BaseUrl is configured (e.g. tests) there is nothing to check.
    /// </summary>
    public sealed class ToolsApiHealthCheck(
        IHttpClientFactory httpClientFactory,
        IOptions<ToolsApiOptions> options) : IHealthCheck
    {
        public const string HttpClientName = "tools-api-health";

        public async Task<HealthCheckResult> CheckHealthAsync(
            HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            var baseUrl = options.Value.BaseUrl;
            if (string.IsNullOrWhiteSpace(baseUrl))
                return HealthCheckResult.Healthy("tools-api not configured");

            try
            {
                var client = httpClientFactory.CreateClient(HttpClientName);
                using var response = await client.GetAsync(
                    new Uri(new Uri(baseUrl), "/health"), cancellationToken);
                return response.IsSuccessStatusCode
                    ? HealthCheckResult.Healthy("tools-api reachable")
                    : HealthCheckResult.Degraded($"tools-api returned {(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                return HealthCheckResult.Degraded("tools-api unreachable", ex);
            }
        }
    }
}
