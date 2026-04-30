using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;
using Odin.Api.Services.Paddle.Models.Common;

namespace Odin.Api.Services.Paddle;

/// <summary>
/// Typed HTTP client for the Paddle API. Handles base URL composition, JSON serialization,
/// error envelope translation, and cursor-based pagination iteration. Cross-cutting concerns
/// (auth, version header, retries) live in <see cref="Http.PaddleAuthHandler"/> and
/// <see cref="Http.PaddleRetryHandler"/> registered as <see cref="DelegatingHandler"/>s.
/// </summary>
public sealed class PaddleApiClient(HttpClient httpClient, IOptionsMonitor<PaddleOptions> options, ILogger<PaddleApiClient> logger) : IPaddleApiClient
{
    public async Task<T> GetAsync<T>(
        string path,
        IReadOnlyDictionary<string, string?>? query = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var envelope = await SendAsync<object?, PaddleEnvelope<T>>(HttpMethod.Get, BuildUri(path, query), body: null, idempotencyKey: null, cancellationToken)
            .ConfigureAwait(false);

        if (envelope?.Data is null)
            throw EmptyEnvelope(envelope?.Meta?.RequestId);

        return envelope.Data;
    }

    public async Task<PaddleListEnvelope<T>> GetPageAsync<T>(
        string path,
        IReadOnlyDictionary<string, string?>? query = null,
        CancellationToken cancellationToken = default) where T : class
    {
        var envelope = await SendAsync<object?, PaddleListEnvelope<T>>(HttpMethod.Get, BuildUri(path, query), body: null, idempotencyKey: null, cancellationToken)
            .ConfigureAwait(false);

        return envelope ?? new PaddleListEnvelope<T>();
    }

    public async IAsyncEnumerable<T> GetAllAsync<T>(
        string path,
        IReadOnlyDictionary<string, string?>? query = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default) where T : class
    {
        var nextUri = BuildUri(path, query);

        while (true)
        {
            var page = await SendAsync<object?, PaddleListEnvelope<T>>(HttpMethod.Get, nextUri, body: null, idempotencyKey: null, cancellationToken)
                .ConfigureAwait(false);

            if (page is null)
                yield break;

            foreach (var item in page.Data)
                yield return item;

            if (page.Meta?.Pagination is { HasMore: true, Next: var next } && !string.IsNullOrEmpty(next))
                nextUri = new Uri(next, UriKind.Absolute);
            else
                yield break;
        }
    }

    public async Task<TResponse> PostAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default) where TResponse : class
    {
        var envelope = await SendAsync<TRequest, PaddleEnvelope<TResponse>>(
            HttpMethod.Post, BuildUri(path, query: null), body, idempotencyKey, cancellationToken).ConfigureAwait(false);

        return envelope?.Data ?? throw EmptyEnvelope(envelope?.Meta?.RequestId);
    }

    public async Task<TResponse> PostAsync<TResponse>(
        string path,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default) where TResponse : class
    {
        var envelope = await SendAsync<object?, PaddleEnvelope<TResponse>>(
            HttpMethod.Post, BuildUri(path, query: null), body: null, idempotencyKey, cancellationToken).ConfigureAwait(false);

        return envelope?.Data ?? throw EmptyEnvelope(envelope?.Meta?.RequestId);
    }

    public async Task<TResponse> PatchAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        string? idempotencyKey = null,
        CancellationToken cancellationToken = default) where TResponse : class
    {
        var envelope = await SendAsync<TRequest, PaddleEnvelope<TResponse>>(
            HttpMethod.Patch, BuildUri(path, query: null), body, idempotencyKey, cancellationToken).ConfigureAwait(false);

        return envelope?.Data ?? throw EmptyEnvelope(envelope?.Meta?.RequestId);
    }

    private async Task<TResult?> SendAsync<TBody, TResult>(
        HttpMethod method,
        Uri uri,
        TBody body,
        string? idempotencyKey,
        CancellationToken cancellationToken) where TResult : class
    {
        using var request = new HttpRequestMessage(method, uri);
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
            request.Headers.Add("Paddle-Idempotency-Key", idempotencyKey);

        if (body is not null && method != HttpMethod.Get)
            request.Content = JsonContent.Create(body, mediaType: new MediaTypeHeaderValue("application/json"), options: PaddleJson.Options);

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
            throw await BuildExceptionAsync(response, cancellationToken).ConfigureAwait(false);

        if (response.Content.Headers.ContentLength == 0)
            return null;

        try
        {
            return await response.Content
                .ReadFromJsonAsync<TResult>(PaddleJson.Options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (JsonException ex)
        {
            var raw = await SafeReadStringAsync(response, cancellationToken).ConfigureAwait(false);
            logger.LogError(ex, "Paddle: failed to deserialize response from {Method} {Uri}. Body: {Body}", method, uri, raw);
            throw new PaddleApiException(response.StatusCode,
                "Failed to deserialize Paddle response body.",
                paddleRequestId: ReadRequestId(response),
                paddleErrorType: null,
                paddleErrorCode: null,
                rawBody: raw,
                inner: ex);
        }
    }

    private Uri BuildUri(string path, IReadOnlyDictionary<string, string?>? query)
    {
        var baseAddress = httpClient.BaseAddress
            ?? new Uri(options.CurrentValue.ApiBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);

        // Allow callers to pass an absolute URL (for instance Paddle's pagination next URL).
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute))
            return AppendQuery(absolute, query);

        var relative = new Uri(path.TrimStart('/'), UriKind.Relative);
        var combined = new Uri(baseAddress, relative);
        return AppendQuery(combined, query);
    }

    private static Uri AppendQuery(Uri uri, IReadOnlyDictionary<string, string?>? query)
    {
        if (query is null || query.Count == 0)
            return uri;

        var builder = new UriBuilder(uri);
        var existing = string.IsNullOrEmpty(builder.Query) ? "" : builder.Query.TrimStart('?');
        var pairs = new List<string>();
        if (!string.IsNullOrEmpty(existing))
            pairs.Add(existing);

        foreach (var (key, value) in query)
        {
            if (string.IsNullOrEmpty(value))
                continue;
            pairs.Add($"{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}");
        }

        builder.Query = string.Join('&', pairs);
        return builder.Uri;
    }

    private static async Task<PaddleApiException> BuildExceptionAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var requestId = ReadRequestId(response);
        var raw = await SafeReadStringAsync(response, cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return new PaddleApiException(response.StatusCode,
                $"Paddle API call failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase} and no response body.",
                paddleRequestId: requestId, paddleErrorType: null, paddleErrorCode: null, rawBody: null);
        }

        try
        {
            var envelope = JsonSerializer.Deserialize<PaddleErrorEnvelope>(raw, PaddleJson.Options);
            var error = envelope?.Error;
            var validation = error?.Errors?
                .Where(e => !string.IsNullOrWhiteSpace(e.Field))
                .Select(e => new PaddleValidationError(e.Field!, e.Message ?? ""))
                .ToList();

            var message = error?.Detail
                ?? $"Paddle API call failed with HTTP {(int)response.StatusCode} {response.ReasonPhrase}.";

            return new PaddleApiException(response.StatusCode, message,
                paddleRequestId: envelope?.Meta?.RequestId ?? requestId,
                paddleErrorType: error?.Type,
                paddleErrorCode: error?.Code,
                rawBody: raw,
                validationErrors: validation);
        }
        catch (JsonException)
        {
            return new PaddleApiException(response.StatusCode,
                $"Paddle API call failed with HTTP {(int)response.StatusCode}; response body was not valid JSON.",
                paddleRequestId: requestId, paddleErrorType: null, paddleErrorCode: null, rawBody: raw);
        }
    }

    private static string? ReadRequestId(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Paddle-Request-Id", out var values) ? values.FirstOrDefault() : null;

    private static async Task<string?> SafeReadStringAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private static PaddleApiException EmptyEnvelope(string? requestId) =>
        new(System.Net.HttpStatusCode.OK,
            "Paddle returned a successful response with no data envelope.",
            paddleRequestId: requestId, paddleErrorType: null, paddleErrorCode: null, rawBody: null);
}
