using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;

namespace Odin.Api.Endpoints.HaplogroupHeatmap
{
    /// <summary>
    /// Typed client over odin-tools-api's <c>/v1/clade-finder/relative-frequency</c> — the live, numpy-backed
    /// kernel-interpolated relative-frequency grid. Unlike <see cref="IHaploGeoExportClient"/> (the paginated
    /// import), this is a per-request compute proxy (like the clade finder), so it shares that base URL /
    /// API key / snake_case JSON but is its own client. The caller passes an <b>already-anchored</b> clade.
    /// </summary>
    public interface IHaplogroupRelativeFrequencyClient
    {
        Task<HaploGeoRelativeFrequencyDto> GetAsync(
            string clade, string layer, double radiusKm, CancellationToken cancellationToken = default);
    }

    public sealed record HaploGeoRelativeFrequencyDto(
        string Clade, string Layer, double RadiusKm, double CellSize,
        string? FrequencyClade, double MaxValue, int CladeCount, int TotalCount,
        List<HaploGeoRfCellDto> Cells);

    public sealed record HaploGeoRfCellDto(double Lat, double Lon, double Value);

    public sealed class HaplogroupRelativeFrequencyClient(
        HttpClient httpClient,
        IOptions<ToolsApiOptions> options,
        ILogger<HaplogroupRelativeFrequencyClient> logger) : IHaplogroupRelativeFrequencyClient
    {
        private readonly ToolsApiOptions _options = options.Value;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
        };

        public async Task<HaploGeoRelativeFrequencyDto> GetAsync(
            string clade, string layer, double radiusKm, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            {
                logger.LogError("ToolsApi:BaseUrl is not configured; cannot reach the relative-frequency grid.");
                throw new InvalidOperationException("Relative-frequency service is not configured.");
            }

            var query = $"clade={Uri.EscapeDataString(clade)}&layer={Uri.EscapeDataString(layer)}" +
                        $"&radius_km={radiusKm.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)}";
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"/v1/clade-finder/relative-frequency?{query}");
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                request.Headers.Add("X-Api-Key", _options.ApiKey.Trim());
            }

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Relative-frequency error {Status}: {Body}", (int)response.StatusCode, body);
                throw new HttpRequestException(
                    $"Relative-frequency returned {(int)response.StatusCode}.", null, response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<HaploGeoRelativeFrequencyDto>(
                JsonOptions, cancellationToken);
            return result ?? throw new InvalidOperationException("Relative-frequency returned an empty response.");
        }
    }
}
