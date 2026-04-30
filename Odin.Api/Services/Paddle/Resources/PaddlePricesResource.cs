using Odin.Api.Services.Paddle.Models.Common;
using Odin.Api.Services.Paddle.Models.Prices;

namespace Odin.Api.Services.Paddle.Resources;

public interface IPaddlePricesResource
{
    Task<PaddleListEnvelope<PaddlePriceDto>> ListAsync(PaddlePriceListQuery? query = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<PaddlePriceDto> ListAllAsync(PaddlePriceListQuery? query = null, CancellationToken cancellationToken = default);
    Task<PaddlePriceDto> GetAsync(string priceId, CancellationToken cancellationToken = default);
}

public sealed class PaddlePricesResource(IPaddleApiClient client) : IPaddlePricesResource
{
    private const string BasePath = "prices";

    public Task<PaddleListEnvelope<PaddlePriceDto>> ListAsync(PaddlePriceListQuery? query = null, CancellationToken cancellationToken = default)
        => client.GetPageAsync<PaddlePriceDto>(BasePath, query?.ToQuery(), cancellationToken);

    public IAsyncEnumerable<PaddlePriceDto> ListAllAsync(PaddlePriceListQuery? query = null, CancellationToken cancellationToken = default)
        => client.GetAllAsync<PaddlePriceDto>(BasePath, query?.ToQuery(), cancellationToken);

    public Task<PaddlePriceDto> GetAsync(string priceId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(priceId);
        return client.GetAsync<PaddlePriceDto>($"{BasePath}/{priceId}", query: null, cancellationToken);
    }
}

public sealed class PaddlePriceListQuery
{
    public string? Status { get; set; }
    public string? ProductId { get; set; }
    public string? Type { get; set; }
    public string? After { get; set; }
    public int? PerPage { get; set; }
    public string? OrderBy { get; set; }

    public Dictionary<string, string?> ToQuery() => new()
    {
        ["status"] = Status,
        ["product_id"] = ProductId,
        ["type"] = Type,
        ["after"] = After,
        ["per_page"] = PerPage?.ToString(),
        ["order_by"] = OrderBy,
    };
}
