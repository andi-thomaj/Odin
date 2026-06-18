using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;

namespace Odin.Api.Endpoints.HaplogroupHeatmap
{
    /// <summary>
    /// Typed client over odin-tools-api's <c>/v1/clade-finder/haplo-geo-export</c> — the paginated
    /// extract of Y-haplogroup samples + tree nodes the import job loads into Postgres. Mirrors
    /// <see cref="CladeFinderManagement.CladeFinderService"/>: same base URL / API key / snake_case JSON.
    /// </summary>
    public interface IHaploGeoExportClient
    {
        Task<HaploGeoExportMeta> GetMetaAsync(CancellationToken cancellationToken = default);
        Task<HaploGeoPage<HaploGeoSampleDto>> GetSamplesAsync(int offset, int limit, CancellationToken cancellationToken = default);
        Task<HaploGeoPage<HaploGeoNodeDto>> GetNodesAsync(int offset, int limit, CancellationToken cancellationToken = default);
        Task<HaploGeoPage<HaploGeoFrequencyDto>> GetFrequenciesAsync(int offset, int limit, CancellationToken cancellationToken = default);
    }

    public sealed record HaploGeoExportMeta(
        string DatasetVersion, int SampleCount, int NodeCount, int UnresolvedCount, int SkippedNoCoords,
        int FrequencyCount = 0);

    public sealed record HaploGeoFrequencyDto(
        string Country, string HcKey, string CladeNodeId, double Percentage, int SampleSize,
        int StudyCount, string? License, string? Source = null, double? Lat = null, double? Lon = null);

    public sealed record HaploGeoSampleDto(
        string GeneticId, string IndividualId, string TreeNodeId,
        string? YTerminal, string? YIsogg, string? YManual,
        double Latitude, double Longitude,
        double? DateMeanBp, double? DateSdBp, string? FullDate,
        string Era, string Layer,
        string? Country, string? Locality, string? GroupId, string? Sex, string? Assessment,
        string? Source = "AADR");

    public sealed record HaploGeoNodeDto(
        string Id, string? ParentId, double? Tmrca, double? Formed, string? Snps,
        double? CentroidLat, double? CentroidLon, int SubtreeSampleCount);

    public sealed record HaploGeoPage<T>(int Offset, int Limit, int Total, List<T> Items);

    public sealed class HaploGeoExportClient(
        HttpClient httpClient,
        IOptions<ToolsApiOptions> options,
        ILogger<HaploGeoExportClient> logger) : IHaploGeoExportClient
    {
        private readonly ToolsApiOptions _options = options.Value;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
        };

        public Task<HaploGeoExportMeta> GetMetaAsync(CancellationToken cancellationToken = default) =>
            GetAsync<HaploGeoExportMeta>("part=meta", cancellationToken);

        public Task<HaploGeoPage<HaploGeoSampleDto>> GetSamplesAsync(
            int offset, int limit, CancellationToken cancellationToken = default) =>
            GetAsync<HaploGeoPage<HaploGeoSampleDto>>($"part=samples&offset={offset}&limit={limit}", cancellationToken);

        public Task<HaploGeoPage<HaploGeoNodeDto>> GetNodesAsync(
            int offset, int limit, CancellationToken cancellationToken = default) =>
            GetAsync<HaploGeoPage<HaploGeoNodeDto>>($"part=nodes&offset={offset}&limit={limit}", cancellationToken);

        public Task<HaploGeoPage<HaploGeoFrequencyDto>> GetFrequenciesAsync(
            int offset, int limit, CancellationToken cancellationToken = default) =>
            GetAsync<HaploGeoPage<HaploGeoFrequencyDto>>($"part=frequencies&offset={offset}&limit={limit}", cancellationToken);

        private async Task<T> GetAsync<T>(string query, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            {
                logger.LogError("ToolsApi:BaseUrl is not configured; cannot reach the haplo-geo export.");
                throw new InvalidOperationException("Haplo-geo export service is not configured.");
            }

            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"/v1/clade-finder/haplo-geo-export?{query}");
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                request.Headers.Add("X-Api-Key", _options.ApiKey.Trim());
            }

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Haplo-geo export error {Status}: {Body}", (int)response.StatusCode, body);
                throw new HttpRequestException(
                    $"Haplo-geo export returned {(int)response.StatusCode}.", null, response.StatusCode);
            }

            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return result ?? throw new InvalidOperationException("Haplo-geo export returned an empty response.");
        }
    }
}
