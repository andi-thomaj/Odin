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
        IHttpClientFactory httpClientFactory,
        IOptions<ToolsApiOptions> options,
        ILogger<MergePipelineService> logger) : IMergePipelineService
    {
        private readonly ToolsApiOptions _options = options.Value;

        /// <summary>Named HttpClient with an infinite per-call timeout, used only for the multi-GB panel
        /// upload — the upload is bounded instead by the endpoint's RequestTimeout policy + the request
        /// cancellation token, so it isn't killed mid-transfer by the 30-min merge client timeout.</summary>
        public const string PanelClientName = "ToolsApiPanelRestore";

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

        public async Task CancelMergeAsync(string mergeId, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            using var request = BuildRequest(HttpMethod.Post, $"/v1/merge/{Uri.EscapeDataString(mergeId)}/cancel");
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Merge cancel API error {Status}: {Body}", (int)response.StatusCode, body);
                throw new MergePipelineException(response.StatusCode, ExtractDetail(body));
            }
        }

        public async Task<PanelStatusResult> GetPanelStatusAsync(
            string? panel, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            var path = "/v1/merge/panel/status" + BuildQuery(("panel", panel));
            using var request = BuildRequest(HttpMethod.Get, path);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            return await ReadPanelStatus(response, path, cancellationToken);
        }

        public async Task<PanelUploadResult> UploadPanelFileAsync(
            string ext, string? panel, string? sha256, Stream body, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            var path = "/v1/merge/panel/restore/upload"
                + BuildQuery(("ext", ext), ("panel", panel), ("sha256", sha256));
            using var request = BuildRequest(HttpMethod.Post, path);
            // StreamContent over the inbound request body: HttpClient sends it chunked, so a multi-GB
            // file never buffers in memory or spools to disk on the .NET side.
            var content = new StreamContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            request.Content = content;

            // The infinite-timeout client; the call is bounded by the caller's cancellationToken
            // (the endpoint's RequestTimeout policy) instead of the 30-min merge client timeout.
            var client = httpClientFactory.CreateClient(PanelClientName);
            using var response = await client.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body2 = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Panel upload API error {Status}: {Body}", (int)response.StatusCode, body2);
                throw new MergePipelineException(response.StatusCode, ExtractDetail(body2));
            }
            var dto = await response.Content.ReadFromJsonAsync<PanelUploadDto>(JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Panel upload API returned an empty response.");
            return new PanelUploadResult(dto.Panel, dto.Ext, dto.StagedSizeBytes, dto.Sha256);
        }

        public async Task<PanelActivateResult> ActivatePanelAsync(
            string? panel, bool force, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();

            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["panel"] = string.IsNullOrWhiteSpace(panel) ? "HO" : panel,
                ["force"] = force ? "true" : "false",
            });
            using var request = BuildRequest(HttpMethod.Post, "/v1/merge/panel/restore/activate");
            request.Content = content;
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogWarning("Panel activate API error {Status}: {Body}", (int)response.StatusCode, body);
                throw new MergePipelineException(response.StatusCode, ExtractDetail(body));
            }
            var dto = await response.Content.ReadFromJsonAsync<PanelActivateDto>(JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException("Panel activate API returned an empty response.");
            return new PanelActivateResult(
                dto.Panel, dto.Ready, dto.Layout, dto.NIndividuals, dto.NSnps, dto.NPopulationLabels,
                dto.Warnings ?? []);
        }

        public async Task<PanelIndRowsResult> GetPanelIndRowsAsync(
            string? panel, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            var path = "/v1/merge/panel/ind" + BuildQuery(("panel", panel));
            using var request = BuildRequest(HttpMethod.Get, path);
            using var response = await httpClient.SendAsync(
                request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var dto = await ReadOrThrow<PanelIndRowsDto>(response, path, cancellationToken);
            var rows = (dto.Rows ?? [])
                .Select(r => new PanelIndRowResult(r.Index, r.Id, r.Sex, r.Label)).ToList();
            return new PanelIndRowsResult(dto.Panel, dto.Prefix, dto.Count, rows);
        }

        public async Task<PanelIndRowResult> SetPanelIndRowLabelAsync(
            string? panel, int index, string label, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            var path = "/v1/merge/panel/ind/row"
                + BuildQuery(("panel", panel), ("index", index.ToString()), ("label", label));
            using var request = BuildRequest(HttpMethod.Put, path);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var dto = await ReadOrThrow<PanelIndRowDto>(response, path, cancellationToken);
            return new PanelIndRowResult(dto.Index, dto.Id, dto.Sex, dto.Label);
        }

        public async Task<PanelRenameLabelResult> RenamePanelLabelAsync(
            string? panel, string fromLabel, string toLabel, CancellationToken cancellationToken = default)
        {
            EnsureConfigured();
            var path = "/v1/merge/panel/ind/rename-label"
                + BuildQuery(("panel", panel), ("from_label", fromLabel), ("to_label", toLabel));
            using var request = BuildRequest(HttpMethod.Post, path);
            using var response = await httpClient.SendAsync(request, cancellationToken);
            var dto = await ReadOrThrow<PanelRenameLabelDto>(response, path, cancellationToken);
            return new PanelRenameLabelResult(dto.Panel, dto.FromLabel, dto.ToLabel, dto.RowsChanged);
        }

        /// <summary>Throw a <see cref="MergePipelineException"/> on a non-success status (preserving the
        /// tools-api detail), else deserialize the JSON body.</summary>
        private async Task<T> ReadOrThrow<T>(HttpResponseMessage response, string path, CancellationToken ct)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("Panel API error {Status} on {Path}: {Body}", (int)response.StatusCode, path, body);
                throw new MergePipelineException(response.StatusCode, ExtractDetail(body));
            }
            return await response.Content.ReadFromJsonAsync<T>(JsonOptions, ct)
                ?? throw new InvalidOperationException($"Panel API returned an empty response from {path}.");
        }

        private async Task<PanelStatusResult> ReadPanelStatus(
            HttpResponseMessage response, string path, CancellationToken cancellationToken)
        {
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                logger.LogError("Panel API error {Status} on {Path}: {Body}", (int)response.StatusCode, path, body);
                throw new MergePipelineException(response.StatusCode, ExtractDetail(body));
            }
            var dto = await response.Content.ReadFromJsonAsync<PanelStatusDto>(JsonOptions, cancellationToken)
                ?? throw new InvalidOperationException($"Panel API returned an empty response from {path}.");
            var files = (dto.Files ?? []).Select(f =>
                new PanelFileStatusResult(f.Ext, f.Present, f.SizeBytes, f.Staged, f.StagedSizeBytes)).ToList();
            return new PanelStatusResult(
                dto.Panel, dto.Prefix, dto.Ready, dto.Layout,
                dto.NIndividuals, dto.NSnps, dto.NPopulationLabels, files);
        }

        /// <summary>Build a <c>?k=v&amp;…</c> query string, URL-escaping values and skipping null/empty ones.</summary>
        private static string BuildQuery(params (string Key, string? Value)[] parameters)
        {
            var parts = parameters
                .Where(p => !string.IsNullOrWhiteSpace(p.Value))
                .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}")
                .ToArray();
            return parts.Length == 0 ? string.Empty : "?" + string.Join("&", parts);
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

        // Panel restore DTOs. Pin the snake_case names that the SnakeCaseLower policy maps ambiguously
        // (digit grouping in "n_snps", the boolean/size fields), mirroring the ConvertDto fix above.
        private sealed record PanelUploadDto(
            string Panel, string Ext,
            [property: JsonPropertyName("staged_size_bytes")] long StagedSizeBytes,
            string Sha256);

        private sealed record PanelFileStatusDto(
            string Ext, bool Present,
            [property: JsonPropertyName("size_bytes")] long? SizeBytes,
            bool Staged,
            [property: JsonPropertyName("staged_size_bytes")] long? StagedSizeBytes);

        private sealed record PanelStatusDto(
            string Panel, string Prefix, bool Ready, string? Layout,
            [property: JsonPropertyName("n_individuals")] int? NIndividuals,
            [property: JsonPropertyName("n_snps")] int? NSnps,
            [property: JsonPropertyName("n_population_labels")] int? NPopulationLabels,
            IReadOnlyList<PanelFileStatusDto>? Files);

        private sealed record PanelActivateDto(
            string Panel, bool Ready, string? Layout,
            [property: JsonPropertyName("n_individuals")] int? NIndividuals,
            [property: JsonPropertyName("n_snps")] int? NSnps,
            [property: JsonPropertyName("n_population_labels")] int? NPopulationLabels,
            IReadOnlyList<string>? Warnings);

        private sealed record PanelIndRowDto(int Index, string Id, string Sex, string Label);

        private sealed record PanelIndRowsDto(
            string Panel, string Prefix, int Count, IReadOnlyList<PanelIndRowDto>? Rows);

        private sealed record PanelRenameLabelDto(
            string Panel,
            [property: JsonPropertyName("from_label")] string FromLabel,
            [property: JsonPropertyName("to_label")] string ToLabel,
            [property: JsonPropertyName("rows_changed")] int RowsChanged);
    }
}
