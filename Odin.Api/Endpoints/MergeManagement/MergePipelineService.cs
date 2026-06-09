using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;

namespace Odin.Api.Endpoints.MergeManagement
{
    public sealed class MergePipelineService(
        HttpClient httpClient,
        IOptions<ToolsApiOptions> options,
        ILogger<MergePipelineService> logger) : IMergePipelineService
    {
        private readonly ToolsApiOptions _options = options.Value;

        // odin-tools-api emits snake_case JSON (converted_23andme, merge_id, size_bytes, ...).
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            PropertyNameCaseInsensitive = true,
        };

        public async Task<ConvertResult> ConvertAsync(
            byte[] raw, string fileName, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            using var content = new MultipartFormDataContent();
            var fileContent = new ByteArrayContent(raw);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            content.Add(fileContent, "file", string.IsNullOrWhiteSpace(fileName) ? "upload.txt" : fileName);

            var dto = await SendAsync<ConvertDto>(HttpMethod.Post, "/v1/merge/convert", content, cancellationToken);
            // System.Text.Json leaves unmatched fields null even on non-nullable record members, so a
            // contract drift (renamed field, wrong casing) yields a null here rather than a parse error.
            // Fail loudly at the boundary instead of letting it blow up deep in the merge job.
            if (string.IsNullOrEmpty(dto.Converted23Andme))
                throw new MergePipelineException(HttpStatusCode.BadGateway,
                    "Merge convert API returned no converted 23andMe data.");
            return new ConvertResult(dto.Converted23Andme, dto.FileName, dto.SourceVendor);
        }

        public async Task<MergeResult> RunMergeAsync(
            string mergeId, string converted23Andme, string? panel, string? sampleId, string sex,
            CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            using var content = new MultipartFormDataContent
            {
                { new StringContent(mergeId), "merge_id" },
                { new StringContent(converted23Andme), "converted_23andme" },
                { new StringContent(string.IsNullOrWhiteSpace(sex) ? "0" : sex), "sex" },
            };
            if (!string.IsNullOrWhiteSpace(panel))
                content.Add(new StringContent(panel), "panel");
            if (!string.IsNullOrWhiteSpace(sampleId))
                content.Add(new StringContent(sampleId), "sample_id");

            var dto = await SendAsync<MergeDto>(HttpMethod.Post, "/v1/merge/run", content, cancellationToken);
            return new MergeResult(dto.MergeId, dto.FileName, dto.SizeBytes, dto.Panel);
        }

        public async Task<HttpResponseMessage> OpenDownloadAsync(
            string mergeId, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            using var request = BuildRequest(HttpMethod.Get, $"/v1/merge/{Uri.EscapeDataString(mergeId)}/download");
            // ResponseHeadersRead: don't buffer the (multi-GB) body — the caller streams it through.
            var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                response.Dispose();
                throw new MergePipelineException(response.StatusCode, ExtractDetail(body));
            }
            return response;
        }

        public async Task DeleteAsync(string mergeId, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            using var request = BuildRequest(HttpMethod.Delete, $"/v1/merge/{Uri.EscapeDataString(mergeId)}");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Merge delete API error {Status}: {Body}", (int)response.StatusCode, body);
                throw new MergePipelineException(response.StatusCode, ExtractDetail(body));
            }
        }

        private async Task<T> SendAsync<T>(
            HttpMethod method, string path, HttpContent content, CancellationToken cancellationToken)
        {
            using var request = BuildRequest(method, path);
            request.Content = content;

            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Merge API error {Status} on {Path}: {Body}", (int)response.StatusCode, path, body);
                throw new MergePipelineException(response.StatusCode, ExtractDetail(body));
            }

            var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
            return result ?? throw new InvalidOperationException($"Merge API returned an empty response from {path}.");
        }

        private HttpRequestMessage BuildRequest(HttpMethod method, string path)
        {
            var request = new HttpRequestMessage(method, path);
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                request.Headers.Add("X-Api-Key", _options.ApiKey.Trim());
            return request;
        }

        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(_options.BaseUrl))
            {
                logger.LogError("ToolsApi:BaseUrl is not configured; cannot reach the merge service.");
                throw new InvalidOperationException("Merge service is not configured.");
            }
        }

        /// <summary>Pull FastAPI's <c>{"detail": "..."}</c> message out of an error body, if present.</summary>
        private static string ExtractDetail(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return "Merge request failed.";
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

        // The snake_case policy maps Converted23Andme → "converted23_andme", but the tools-api emits
        // "converted_23andme" (digit grouped with "23andme"). Pin it explicitly so the field binds —
        // otherwise it deserializes to null and the merge job throws on Encoding.GetBytes(null).
        private sealed record ConvertDto(
            [property: JsonPropertyName("converted_23andme")] string Converted23Andme,
            string FileName,
            string SourceVendor);
        private sealed record MergeDto(string MergeId, string FileName, long SizeBytes, string Panel);
    }
}
