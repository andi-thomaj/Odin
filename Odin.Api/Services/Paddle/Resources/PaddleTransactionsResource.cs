using Odin.Api.Services.Paddle.Models.Common;
using Odin.Api.Services.Paddle.Models.Transactions;

namespace Odin.Api.Services.Paddle.Resources;

public interface IPaddleTransactionsResource
{
    Task<PaddleListEnvelope<PaddleTransactionDto>> ListAsync(PaddleTransactionListQuery? query = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<PaddleTransactionDto> ListAllAsync(PaddleTransactionListQuery? query = null, CancellationToken cancellationToken = default);
    Task<PaddleTransactionDto> GetAsync(string transactionId, PaddleTransactionGetOptions? options = null, CancellationToken cancellationToken = default);
}

public sealed class PaddleTransactionsResource(IPaddleApiClient client) : IPaddleTransactionsResource
{
    private const string BasePath = "transactions";

    public Task<PaddleListEnvelope<PaddleTransactionDto>> ListAsync(PaddleTransactionListQuery? query = null, CancellationToken cancellationToken = default)
        => client.GetPageAsync<PaddleTransactionDto>(BasePath, query?.ToQuery(), cancellationToken);

    public IAsyncEnumerable<PaddleTransactionDto> ListAllAsync(PaddleTransactionListQuery? query = null, CancellationToken cancellationToken = default)
        => client.GetAllAsync<PaddleTransactionDto>(BasePath, query?.ToQuery(), cancellationToken);

    public Task<PaddleTransactionDto> GetAsync(string transactionId, PaddleTransactionGetOptions? options = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transactionId);
        return client.GetAsync<PaddleTransactionDto>($"{BasePath}/{transactionId}", options?.ToQuery(), cancellationToken);
    }
}

public sealed class PaddleTransactionListQuery
{
    public string? Status { get; set; }
    public string? CustomerId { get; set; }
    public string? SubscriptionId { get; set; }
    public string? Origin { get; set; }
    public string? CollectionMode { get; set; }
    public DateTimeOffset? BilledAtFrom { get; set; }
    public DateTimeOffset? BilledAtTo { get; set; }
    public string? After { get; set; }
    public int? PerPage { get; set; }
    public string? OrderBy { get; set; }

    public Dictionary<string, string?> ToQuery() => new()
    {
        ["status"] = Status,
        ["customer_id"] = CustomerId,
        ["subscription_id"] = SubscriptionId,
        ["origin"] = Origin,
        ["collection_mode"] = CollectionMode,
        ["billed_at[GTE]"] = BilledAtFrom?.ToString("O"),
        ["billed_at[LTE]"] = BilledAtTo?.ToString("O"),
        ["after"] = After,
        ["per_page"] = PerPage?.ToString(),
        ["order_by"] = OrderBy,
    };
}

public sealed class PaddleTransactionGetOptions
{
    /// <summary>Comma-separated set of <c>customer</c>, <c>address</c>, <c>business</c>, <c>discount</c>, <c>adjustments</c>, <c>adjustments_totals</c>.</summary>
    public string? Include { get; set; }

    public Dictionary<string, string?> ToQuery() => new()
    {
        ["include"] = Include,
    };
}
