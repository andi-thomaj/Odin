using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;
using Odin.Api.Endpoints.CladeFinderManagement.Models;

namespace Odin.Api.Endpoints.CladeFinderManagement
{
    public sealed class CladeFinderService(
        HttpClient httpClient,
        IOptions<ToolsApiOptions> options,
        ILogger<CladeFinderService> logger) : ICladeFinderService
    {
        private readonly ToolsApiOptions _options = options.Value;

        // odin-tools-api emits snake_case JSON (e.g. next_prediction, positives_used, source_format).
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
        };

        public async Task<AnalyzeCladeContract.Response> AnalyzeAsync(
            IFormFile file, string? build, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            {
                logger.LogError("ToolsApi:BaseUrl is not configured; cannot reach the clade finder.");
                throw new InvalidOperationException("Clade finder service is not configured.");
            }

            using var content = new MultipartFormDataContent();
            var fileContent = new StreamContent(file.OpenReadStream());
            if (!string.IsNullOrWhiteSpace(file.ContentType))
            {
                fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            }

            content.Add(fileContent, "file", file.FileName);
            if (!string.IsNullOrWhiteSpace(build))
            {
                content.Add(new StringContent(build), "build");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/clade-finder/analyze")
            {
                Content = content,
            };
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
            {
                request.Headers.Add("X-Api-Key", _options.ApiKey.Trim());
            }

            var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Clade finder API error {Status}: {Body}", (int)response.StatusCode, body);
                throw new CladeFinderException(response.StatusCode, ExtractDetail(body));
            }

            var result = await response.Content.ReadFromJsonAsync<AnalyzeCladeContract.Response>(
                JsonOptions, cancellationToken);
            return result ?? throw new InvalidOperationException("Clade finder returned an empty response.");
        }

        /// <summary>Pull FastAPI's <c>{"detail": "..."}</c> message out of an error body, if present.</summary>
        private static string ExtractDetail(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return "Clade finder request failed.";
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind == JsonValueKind.Object
                    && doc.RootElement.TryGetProperty("detail", out var detail)
                    && detail.ValueKind == JsonValueKind.String)
                {
                    return detail.GetString() ?? body;
                }
            }
            catch (JsonException)
            {
                // not JSON; fall through
            }

            return body;
        }
    }

    /// <summary>Raised when the tools API returns a non-success status, preserving the status code.</summary>
    public sealed class CladeFinderException(HttpStatusCode statusCode, string detail) : Exception(detail)
    {
        public HttpStatusCode StatusCode { get; } = statusCode;
        public string Detail { get; } = detail;
    }
}
