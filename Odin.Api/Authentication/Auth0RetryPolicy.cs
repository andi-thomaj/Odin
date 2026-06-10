namespace Odin.Api.Authentication;

/// <summary>
/// Shared retry wrapper for Auth0 calls (<c>/userinfo</c>, email-verified). Retries on HTTP 429 and
/// transient 5xx, and on transient network faults (<see cref="HttpRequestException"/> / request
/// timeout), with jittered backoff to avoid a thundering herd when Auth0 briefly throttles. A
/// caller-requested cancellation is never retried. Replaces the hand-rolled, 429-only loops that were
/// duplicated in <see cref="Middleware.RoleEnrichmentMiddleware"/> and
/// <see cref="Services.UserProvisioningService"/>.
/// </summary>
public static class Auth0RetryPolicy
{
    /// <summary>
    /// Run <paramref name="operation"/> (which returns its result and the HTTP status code it observed),
    /// retrying transient failures up to <paramref name="maxAttempts"/> times. Returns the last outcome;
    /// a transient exception on the final attempt is rethrown for the caller to handle.
    /// </summary>
    public static async Task<(T Result, int StatusCode)> ExecuteAsync<T>(
        Func<CancellationToken, Task<(T Result, int StatusCode)>> operation,
        ILogger logger,
        int maxAttempts = 3,
        CancellationToken cancellationToken = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                var outcome = await operation(cancellationToken).ConfigureAwait(false);
                if (outcome.StatusCode == 200 || !IsRetriableStatus(outcome.StatusCode) || attempt >= maxAttempts)
                    return outcome;
                logger.LogDebug(
                    "Auth0 call returned {Status}; retrying ({Attempt}/{Max}).",
                    outcome.StatusCode, attempt, maxAttempts);
            }
            catch (Exception ex)
                when (IsTransient(ex) && !cancellationToken.IsCancellationRequested && attempt < maxAttempts)
            {
                logger.LogDebug(ex, "Auth0 call failed transiently; retrying ({Attempt}/{Max}).", attempt, maxAttempts);
            }

            await Task.Delay(BackoffWithJitter(attempt), cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsRetriableStatus(int status) => status == 429 || status >= 500;

    private static bool IsTransient(Exception ex) =>
        // HttpClient request timeout surfaces as TaskCanceledException; the caller-cancel case is
        // excluded by the cancellationToken check at the call site.
        ex is HttpRequestException or TaskCanceledException or TimeoutException;

    private static TimeSpan BackoffWithJitter(int attempt)
    {
        // 100ms, 200ms, 300ms ... plus up to 50% jitter, so simultaneous callers don't retry in lockstep.
        var baseMs = 100 * attempt;
        var jitterMs = Random.Shared.Next(0, baseMs / 2 + 1);
        return TimeSpan.FromMilliseconds(baseMs + jitterMs);
    }
}
