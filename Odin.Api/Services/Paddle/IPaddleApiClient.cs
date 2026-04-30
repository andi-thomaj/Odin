using Odin.Api.Services.Paddle.Models.Common;

namespace Odin.Api.Services.Paddle;

/// <summary>
/// Low-level transport for the Paddle API. Resource services (products, prices, customers,
/// subscriptions, transactions, notifications) compose on top of this client and translate
/// raw Paddle DTOs into domain entities.
/// </summary>
public interface IPaddleApiClient
{
    /// <summary>Fetches a single entity from a Paddle GET endpoint.</summary>
    Task<T> GetAsync<T>(string path, IReadOnlyDictionary<string, string?>? query = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>Fetches one page from a Paddle list endpoint.</summary>
    Task<PaddleListEnvelope<T>> GetPageAsync<T>(string path, IReadOnlyDictionary<string, string?>? query = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// Iterates every page of a Paddle list endpoint, following the cursor in <c>meta.pagination.next</c>.
    /// Yields one item per row across all pages.
    /// </summary>
    IAsyncEnumerable<T> GetAllAsync<T>(string path, IReadOnlyDictionary<string, string?>? query = null, CancellationToken cancellationToken = default) where T : class;

    /// <summary>POSTs a write request. Pass <paramref name="idempotencyKey"/> to enable safe retries.</summary>
    Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body, string? idempotencyKey = null, CancellationToken cancellationToken = default) where TResponse : class;

    /// <summary>POSTs without a request body (used for replay-style endpoints). Idempotency key recommended.</summary>
    Task<TResponse> PostAsync<TResponse>(string path, string? idempotencyKey = null, CancellationToken cancellationToken = default) where TResponse : class;

    /// <summary>PATCHes an existing entity.</summary>
    Task<TResponse> PatchAsync<TRequest, TResponse>(string path, TRequest body, string? idempotencyKey = null, CancellationToken cancellationToken = default) where TResponse : class;
}
