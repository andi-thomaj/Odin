using System.Net;
using Microsoft.Extensions.Options;
using Odin.Api.Configuration;

namespace Odin.Api.Services.Paddle.Http;

/// <summary>
/// Retries idempotent Paddle calls on transient failures (5xx, 429) with exponential backoff.
/// Honours the <c>Retry-After</c> header on 429 responses. Does not retry POST/PATCH unless the
/// caller has supplied a <c>Paddle-Idempotency-Key</c> — Paddle uses that key to dedupe writes.
/// </summary>
public sealed class PaddleRetryHandler(IOptionsMonitor<PaddleOptions> options) : DelegatingHandler
{
    private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var maxRetries = Math.Max(0, options.CurrentValue.MaxRetries);
        var canRetry = IsRetryableMethod(request);

        HttpResponseMessage? response = null;
        Exception? lastException = null;

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            if (attempt > 0)
                await Task.Delay(ComputeDelay(attempt, response), cancellationToken).ConfigureAwait(false);

            response?.Dispose();

            try
            {
                response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                lastException = null;
            }
            catch (HttpRequestException ex) when (canRetry && attempt < maxRetries)
            {
                lastException = ex;
                continue;
            }
            catch (TaskCanceledException ex) when (canRetry && attempt < maxRetries && !cancellationToken.IsCancellationRequested)
            {
                lastException = ex;
                continue;
            }

            if (!canRetry || attempt == maxRetries || !IsTransientStatus(response.StatusCode))
                return response;
        }

        if (lastException is not null)
            throw lastException;

        return response!;
    }

    private static bool IsRetryableMethod(HttpRequestMessage request)
    {
        if (request.Method == HttpMethod.Get
            || request.Method == HttpMethod.Head
            || request.Method == HttpMethod.Options
            || request.Method == HttpMethod.Delete)
        {
            return true;
        }

        // Writes are only safe to retry when the caller supplied an idempotency key.
        return request.Headers.Contains("Paddle-Idempotency-Key");
    }

    private static bool IsTransientStatus(HttpStatusCode status) =>
        (int)status >= 500 || status == HttpStatusCode.TooManyRequests || status == HttpStatusCode.RequestTimeout;

    private static TimeSpan ComputeDelay(int attempt, HttpResponseMessage? response)
    {
        if (response?.Headers.RetryAfter is { } retryAfter)
        {
            if (retryAfter.Delta is { } delta)
                return Clamp(delta);
            if (retryAfter.Date is { } date)
            {
                var diff = date - DateTimeOffset.UtcNow;
                if (diff > TimeSpan.Zero)
                    return Clamp(diff);
            }
        }

        var exponential = TimeSpan.FromMilliseconds(BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1));
        var jitter = TimeSpan.FromMilliseconds(Random.Shared.Next(0, 200));
        return Clamp(exponential + jitter);
    }

    private static TimeSpan Clamp(TimeSpan value) =>
        value > MaxDelay ? MaxDelay : value < TimeSpan.Zero ? TimeSpan.Zero : value;
}
