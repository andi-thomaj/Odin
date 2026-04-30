using Odin.Api.Services.Paddle.Models.Common;
using Odin.Api.Services.Paddle.Models.Subscriptions;

namespace Odin.Api.Services.Paddle.Resources;

public interface IPaddleSubscriptionsResource
{
    Task<PaddleListEnvelope<PaddleSubscriptionDto>> ListAsync(PaddleSubscriptionListQuery? query = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<PaddleSubscriptionDto> ListAllAsync(PaddleSubscriptionListQuery? query = null, CancellationToken cancellationToken = default);
    Task<PaddleSubscriptionDto> GetAsync(string subscriptionId, CancellationToken cancellationToken = default);
}

public sealed class PaddleSubscriptionsResource(IPaddleApiClient client) : IPaddleSubscriptionsResource
{
    private const string BasePath = "subscriptions";

    public Task<PaddleListEnvelope<PaddleSubscriptionDto>> ListAsync(PaddleSubscriptionListQuery? query = null, CancellationToken cancellationToken = default)
        => client.GetPageAsync<PaddleSubscriptionDto>(BasePath, query?.ToQuery(), cancellationToken);

    public IAsyncEnumerable<PaddleSubscriptionDto> ListAllAsync(PaddleSubscriptionListQuery? query = null, CancellationToken cancellationToken = default)
        => client.GetAllAsync<PaddleSubscriptionDto>(BasePath, query?.ToQuery(), cancellationToken);

    public Task<PaddleSubscriptionDto> GetAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subscriptionId);
        return client.GetAsync<PaddleSubscriptionDto>($"{BasePath}/{subscriptionId}", query: null, cancellationToken);
    }
}

public sealed class PaddleSubscriptionListQuery
{
    public string? Status { get; set; }
    public string? CustomerId { get; set; }
    public string? PriceId { get; set; }
    public string? CollectionMode { get; set; }
    public string? After { get; set; }
    public int? PerPage { get; set; }
    public string? OrderBy { get; set; }

    public Dictionary<string, string?> ToQuery() => new()
    {
        ["status"] = Status,
        ["customer_id"] = CustomerId,
        ["price_id"] = PriceId,
        ["collection_mode"] = CollectionMode,
        ["after"] = After,
        ["per_page"] = PerPage?.ToString(),
        ["order_by"] = OrderBy,
    };
}
