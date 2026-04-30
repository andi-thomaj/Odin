using Odin.Api.Services.Paddle.Models.Common;
using Odin.Api.Services.Paddle.Models.Products;

namespace Odin.Api.Services.Paddle.Resources;

public interface IPaddleProductsResource
{
    Task<PaddleListEnvelope<PaddleProductDto>> ListAsync(PaddleProductListQuery? query = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<PaddleProductDto> ListAllAsync(PaddleProductListQuery? query = null, CancellationToken cancellationToken = default);
    Task<PaddleProductDto> GetAsync(string productId, bool includePrices = false, CancellationToken cancellationToken = default);
    Task<PaddleProductDto> CreateAsync(PaddleCreateProductRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default);
    Task<PaddleProductDto> UpdateAsync(string productId, PaddleUpdateProductRequest request, CancellationToken cancellationToken = default);
}

public sealed class PaddleProductsResource(IPaddleApiClient client) : IPaddleProductsResource
{
    private const string BasePath = "products";

    public Task<PaddleListEnvelope<PaddleProductDto>> ListAsync(PaddleProductListQuery? query = null, CancellationToken cancellationToken = default)
        => client.GetPageAsync<PaddleProductDto>(BasePath, query?.ToQuery(), cancellationToken);

    public IAsyncEnumerable<PaddleProductDto> ListAllAsync(PaddleProductListQuery? query = null, CancellationToken cancellationToken = default)
        => client.GetAllAsync<PaddleProductDto>(BasePath, query?.ToQuery(), cancellationToken);

    public Task<PaddleProductDto> GetAsync(string productId, bool includePrices = false, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        var query = includePrices ? new Dictionary<string, string?> { ["include"] = "prices" } : null;
        return client.GetAsync<PaddleProductDto>($"{BasePath}/{productId}", query, cancellationToken);
    }

    public Task<PaddleProductDto> CreateAsync(PaddleCreateProductRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        => client.PostAsync<PaddleCreateProductRequest, PaddleProductDto>(BasePath, request, idempotencyKey, cancellationToken);

    public Task<PaddleProductDto> UpdateAsync(string productId, PaddleUpdateProductRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(productId);
        return client.PatchAsync<PaddleUpdateProductRequest, PaddleProductDto>($"{BasePath}/{productId}", request, idempotencyKey: null, cancellationToken);
    }
}

public sealed class PaddleProductListQuery
{
    public string? Status { get; set; }
    public string? Type { get; set; }
    public string? Id { get; set; }
    public string? After { get; set; }
    public int? PerPage { get; set; }
    public string? OrderBy { get; set; }
    public bool IncludePrices { get; set; }

    public Dictionary<string, string?> ToQuery() => new()
    {
        ["status"] = Status,
        ["type"] = Type,
        ["id"] = Id,
        ["after"] = After,
        ["per_page"] = PerPage?.ToString(),
        ["order_by"] = OrderBy,
        ["include"] = IncludePrices ? "prices" : null,
    };
}
