using System.ClientModel;
using System.ClientModel.Primitives;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;
using OpenAI;
using OpenAI.Images;

// The gpt-image-2 generation options (Background, OutputFileFormat, OutputCompressionFactor, ModerationLevel,
// GeneratedImageSize.Auto, GeneratedImageCollection.Usage) are flagged [Experimental("OPENAI001")] by the SDK.
// They are the exact, current gpt-image-2 surface we need; acknowledge the diagnostic for this file only.
#pragma warning disable OPENAI001

namespace Odin.Api.Endpoints.ImageGenerationManagement;

/// <summary>
/// OpenAI image client backed by the official <c>OpenAI</c> .NET SDK for generation/editing, plus a small
/// HttpClient for the Administration usage/cost endpoints (which the SDK doesn't cover).
/// <para>
/// gpt-image-2 gotchas honoured here: never send <c>response_format</c> (the API rejects it and always
/// returns base64); construct the quality/size/format/background/moderation values from the canonical
/// strings rather than the SDK's <c>GeneratedImageQuality.High</c> (which serialises to DALL-E's "hd").
/// Generation uses the typed SDK method; multi-image edits go through the SDK's protocol method because the
/// typed edit overloads accept only a single input image.
/// </para>
/// </summary>
public sealed class OpenAIImageClient : IOpenAIImageClient
{
    private readonly HttpClient _httpClient;
    private readonly OpenAIOptions _options;
    private readonly ILogger<OpenAIImageClient> _logger;
    private ImageClient? _imageClient;

    public OpenAIImageClient(HttpClient httpClient, IOptions<OpenAIOptions> options, ILogger<OpenAIImageClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    private ImageClient ImageClient => _imageClient ??= CreateImageClient();

    private ImageClient CreateImageClient()
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new OpenAIImageException(
                HttpStatusCode.ServiceUnavailable, "OpenAI image generation is not configured (missing OpenAI:ApiKey).");
        }

        var clientOptions = new OpenAIClientOptions
        {
            NetworkTimeout = TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)),
        };
        // Only override the SDK's default endpoint (https://api.openai.com/v1) for a proxy/gateway.
        if (!string.IsNullOrWhiteSpace(_options.BaseUrl)
            && !_options.BaseUrl.Contains("api.openai.com", StringComparison.OrdinalIgnoreCase))
        {
            clientOptions.Endpoint = new Uri(_options.BaseUrl.TrimEnd('/') + "/v1");
        }

        return new ImageClient(_options.GenerationModel, new ApiKeyCredential(_options.ApiKey.Trim()), clientOptions);
    }

    public async Task<OpenAIImageResult> GenerateAsync(
        OpenAIGenerateRequest request, CancellationToken cancellationToken = default)
    {
        var p = request.Parameters;
        var options = BuildGenerationOptions(p);
        try
        {
            ClientResult<GeneratedImageCollection> result =
                await ImageClient.GenerateImagesAsync(request.Prompt, p.N, options, cancellationToken);
            return MapCollection(result.Value);
        }
        catch (ClientResultException ex)
        {
            throw MapSdkException(ex);
        }
    }

    public async Task<OpenAIImageResult> EditAsync(OpenAIEditRequest request, CancellationToken cancellationToken = default)
    {
        var p = request.Parameters;

        using var form = new MultipartFormDataContent
        {
            { new StringContent(p.Model), "model" },
            { new StringContent(request.Prompt), "prompt" },
            { new StringContent(p.N.ToString(CultureInfo.InvariantCulture)), "n" },
            { new StringContent(p.Size), "size" },
            { new StringContent(p.Quality), "quality" },
            { new StringContent(p.Background), "background" },
            { new StringContent(p.OutputFormat), "output_format" },
            { new StringContent(p.Moderation), "moderation" },
        };

        // gpt-image-2 takes up to 16 reference images via repeated image[] parts.
        foreach (var image in request.Images)
        {
            var part = new ByteArrayContent(image.Bytes);
            part.Headers.ContentType = new MediaTypeHeaderValue(image.ContentType);
            form.Add(part, "image[]", image.FileName);
        }

        if (request.Mask is { } mask)
        {
            var maskPart = new ByteArrayContent(mask.Bytes);
            maskPart.Headers.ContentType = new MediaTypeHeaderValue(mask.ContentType);
            form.Add(maskPart, "mask", mask.FileName);
        }

        if (p.OutputCompression is { } compression && p.OutputFormat is "jpeg" or "webp")
        {
            form.Add(new StringContent(compression.ToString(CultureInfo.InvariantCulture)), "output_compression");
        }

        if (!string.IsNullOrWhiteSpace(request.InputFidelity))
        {
            form.Add(new StringContent(request.InputFidelity), "input_fidelity");
        }

        if (!string.IsNullOrWhiteSpace(p.EndUserId))
        {
            form.Add(new StringContent(p.EndUserId), "user");
        }

        var bodyBytes = await form.ReadAsByteArrayAsync(cancellationToken);
        var contentType = form.Headers.ContentType!.ToString();

        try
        {
            using var content = BinaryContent.Create(BinaryData.FromBytes(bodyBytes));
            ClientResult result = await ImageClient.GenerateImageEditsAsync(
                content, contentType, new RequestOptions { CancellationToken = cancellationToken });
            return ParseImageResponse(result.GetRawResponse().Content);
        }
        catch (ClientResultException ex)
        {
            throw MapSdkException(ex);
        }
    }

    private ImageGenerationOptions BuildGenerationOptions(OpenAIImageParameters p)
    {
        var options = new ImageGenerationOptions
        {
            // Construct from canonical strings so "high" maps to gpt-image-2's "high" (NOT GeneratedImageQuality.High = "hd").
            Quality = new GeneratedImageQuality(p.Quality),
            Size = ParseSize(p.Size),
            Background = new GeneratedImageBackground(p.Background),
            OutputFileFormat = new GeneratedImageFileFormat(p.OutputFormat),
            ModerationLevel = new GeneratedImageModerationLevel(p.Moderation),
            // ResponseFormat is deliberately left unset — gpt-image-2 rejects response_format and returns base64.
        };

        if (p.OutputCompression is { } compression && p.OutputFormat is "jpeg" or "webp")
        {
            options.OutputCompressionFactor = compression;
        }

        if (!string.IsNullOrWhiteSpace(p.EndUserId))
        {
            options.EndUserId = p.EndUserId;
        }

        return options;
    }

    private static GeneratedImageSize ParseSize(string size)
    {
        if (string.Equals(size, "auto", StringComparison.OrdinalIgnoreCase))
        {
            return GeneratedImageSize.Auto;
        }

        var parts = size.Split('x', 2);
        if (parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var w)
            && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h))
        {
            return new GeneratedImageSize(w, h);
        }

        return GeneratedImageSize.Auto;
    }

    private OpenAIImageResult MapCollection(GeneratedImageCollection collection)
    {
        var images = new List<OpenAIGeneratedImageBytes>(collection.Count);
        for (var i = 0; i < collection.Count; i++)
        {
            var image = collection[i];
            if (image.ImageBytes is null)
            {
                throw new OpenAIImageException(
                    HttpStatusCode.BadGateway, "OpenAI returned an image without bytes (unexpected URL response).");
            }

            images.Add(new OpenAIGeneratedImageBytes(image.ImageBytes.ToArray(), image.RevisedPrompt));
        }

        OpenAIImageUsage? usage = collection.Usage is { } u
            ? new OpenAIImageUsage(u.InputTokenCount, u.OutputTokenCount, u.TotalTokenCount)
            : null;

        return new OpenAIImageResult(images, usage);
    }

    /// <summary>Parse the raw JSON of an images response (used for the edits protocol call).</summary>
    private static OpenAIImageResult ParseImageResponse(BinaryData body)
    {
        using var doc = JsonDocument.Parse(body.ToString());
        var root = doc.RootElement;

        var images = new List<OpenAIGeneratedImageBytes>();
        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (item.TryGetProperty("b64_json", out var b64) && b64.ValueKind == JsonValueKind.String)
                {
                    var bytes = Convert.FromBase64String(b64.GetString()!);
                    var revised = item.TryGetProperty("revised_prompt", out var rp) && rp.ValueKind == JsonValueKind.String
                        ? rp.GetString()
                        : null;
                    images.Add(new OpenAIGeneratedImageBytes(bytes, revised));
                }
            }
        }

        OpenAIImageUsage? usage = null;
        if (root.TryGetProperty("usage", out var u) && u.ValueKind == JsonValueKind.Object)
        {
            usage = new OpenAIImageUsage(
                ReadLong(u, "input_tokens"),
                ReadLong(u, "output_tokens"),
                ReadLong(u, "total_tokens"));
        }

        return new OpenAIImageResult(images, usage);
    }

    public async Task<OpenAICompletionsUsageReport> GetCompletionsUsageAsync(
        string model, DateTimeOffset start, DateTimeOffset? end, string bucketWidth, CancellationToken cancellationToken = default)
    {
        EnsureAdminConfigured();

        var buckets = new List<OpenAIUsageBucket>();
        string? page = null;

        // gpt-image-2 is a GPT model — its usage is on the completions endpoint, filtered to the model.
        for (var i = 0; i < 50; i++)
        {
            var query = new List<string>
            {
                $"start_time={start.ToUnixTimeSeconds()}",
                $"bucket_width={Uri.EscapeDataString(bucketWidth)}",
                $"limit={MaxLimitForBucket(bucketWidth)}",
                $"models={Uri.EscapeDataString(model)}",
            };
            if (end is { } e) query.Add($"end_time={e.ToUnixTimeSeconds()}");
            if (page is not null) query.Add($"page={Uri.EscapeDataString(page)}");

            using var doc = await SendAdminGetAsync(
                "/v1/organization/usage/completions?" + string.Join("&", query), cancellationToken);
            var root = doc.RootElement;

            foreach (var bucket in EnumerateData(root))
            {
                var startTime = ReadLong(bucket, "start_time") ?? 0;
                var endTime = ReadLong(bucket, "end_time") ?? startTime;
                long requestCount = 0, inputTokens = 0, outputTokens = 0;
                if (bucket.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in results.EnumerateArray())
                    {
                        requestCount += ReadLong(r, "num_model_requests") ?? 0;
                        inputTokens += ReadLong(r, "input_tokens") ?? 0;
                        outputTokens += ReadLong(r, "output_tokens") ?? 0;
                    }
                }

                buckets.Add(new OpenAIUsageBucket(startTime, endTime, requestCount, inputTokens, outputTokens));
            }

            page = NextPage(root);
            if (page is null) break;
        }

        return new OpenAICompletionsUsageReport(
            buckets,
            buckets.Sum(b => b.Requests),
            buckets.Sum(b => b.InputTokens),
            buckets.Sum(b => b.OutputTokens));
    }

    public async Task<OpenAICostReport> GetCostsAsync(
        DateTimeOffset start, DateTimeOffset? end, CancellationToken cancellationToken = default)
    {
        EnsureAdminConfigured();

        var buckets = new List<OpenAICostBucket>();
        var currency = "usd";
        string? page = null;

        for (var i = 0; i < 50; i++)
        {
            var query = new List<string>
            {
                $"start_time={start.ToUnixTimeSeconds()}",
                "bucket_width=1d", // the costs endpoint only supports daily buckets
                "limit=31", // the Administration API caps limit at 31 for 1d buckets
            };
            if (end is { } e) query.Add($"end_time={e.ToUnixTimeSeconds()}");
            if (page is not null) query.Add($"page={Uri.EscapeDataString(page)}");

            using var doc = await SendAdminGetAsync("/v1/organization/costs?" + string.Join("&", query), cancellationToken);
            var root = doc.RootElement;

            foreach (var bucket in EnumerateData(root))
            {
                var startTime = ReadLong(bucket, "start_time") ?? 0;
                var endTime = ReadLong(bucket, "end_time") ?? startTime;
                decimal amount = 0;
                if (bucket.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
                {
                    foreach (var r in results.EnumerateArray())
                    {
                        if (r.TryGetProperty("amount", out var a) && a.ValueKind == JsonValueKind.Object)
                        {
                            if (a.TryGetProperty("value", out var v) && v.ValueKind == JsonValueKind.Number)
                            {
                                amount += v.GetDecimal();
                            }
                            if (a.TryGetProperty("currency", out var c) && c.ValueKind == JsonValueKind.String)
                            {
                                currency = c.GetString() ?? currency;
                            }
                        }
                    }
                }

                buckets.Add(new OpenAICostBucket(startTime, endTime, amount));
            }

            page = NextPage(root);
            if (page is null) break;
        }

        return new OpenAICostReport(buckets, buckets.Sum(b => b.AmountUsd), currency);
    }

    private async Task<JsonDocument> SendAdminGetAsync(string requestUri, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.AdminApiKey.Trim());

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OpenAI Administration API error {Status}: {Body}", (int)response.StatusCode, body);
            throw MapErrorBody(response.StatusCode, body);
        }

        return JsonDocument.Parse(body);
    }

    /// <summary>
    /// Max page size the Administration API allows for a given bucket width: 1440 for <c>1m</c>, 168 for
    /// <c>1h</c>, 31 for <c>1d</c>. Exceeding it returns a 400; longer ranges are walked via <c>next_page</c>.
    /// </summary>
    private static int MaxLimitForBucket(string bucketWidth) => bucketWidth switch
    {
        "1m" => 1440,
        "1h" => 168,
        _ => 31,
    };

    private void EnsureAdminConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.AdminApiKey))
        {
            throw new OpenAIImageException(
                HttpStatusCode.ServiceUnavailable, "OpenAI usage reporting is not configured (missing OpenAI:AdminApiKey).");
        }
    }

    private static IEnumerable<JsonElement> EnumerateData(JsonElement root) =>
        root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array
            ? data.EnumerateArray()
            : [];

    private static string? NextPage(JsonElement root)
    {
        var hasMore = root.TryGetProperty("has_more", out var hm) && hm.ValueKind == JsonValueKind.True;
        if (!hasMore) return null;
        return root.TryGetProperty("next_page", out var np) && np.ValueKind == JsonValueKind.String ? np.GetString() : null;
    }

    private static long? ReadLong(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt64()
            : null;

    /// <summary>Map an SDK <see cref="ClientResultException"/> to our exception, detecting moderation blocks.</summary>
    private static OpenAIImageException MapSdkException(ClientResultException ex)
    {
        string? body = null;
        try { body = ex.GetRawResponse()?.Content?.ToString(); }
        catch { /* best effort */ }

        return MapErrorBody((HttpStatusCode)ex.Status, body, ex.Message);
    }

    private static OpenAIImageException MapErrorBody(HttpStatusCode statusCode, string? body, string? fallbackMessage = null)
    {
        string? code = null;
        var message = fallbackMessage;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
                {
                    if (error.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String)
                        code = c.GetString();
                    if (error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                        message = m.GetString();
                }
            }
            catch (JsonException)
            {
                // not JSON; keep fallback
            }
        }

        var isModeration = string.Equals(code, "moderation_blocked", StringComparison.OrdinalIgnoreCase);
        return new OpenAIImageException(
            statusCode,
            message ?? "The OpenAI image service returned an error.",
            isModeration,
            code);
    }
}
