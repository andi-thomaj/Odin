using Odin.Api.Services.Paddle.Models.Common;
using Odin.Api.Services.Paddle.Models.Customers;

namespace Odin.Api.Services.Paddle.Resources;

public interface IPaddleCustomersResource
{
    Task<PaddleListEnvelope<PaddleCustomerDto>> ListAsync(PaddleCustomerListQuery? query = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<PaddleCustomerDto> ListAllAsync(PaddleCustomerListQuery? query = null, CancellationToken cancellationToken = default);
    Task<PaddleCustomerDto> GetAsync(string customerId, CancellationToken cancellationToken = default);
    Task<PaddleCustomerDto> CreateAsync(PaddleCreateCustomerRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default);
    Task<PaddleCustomerDto> UpdateAsync(string customerId, PaddleUpdateCustomerRequest request, CancellationToken cancellationToken = default);
}

public sealed class PaddleCustomersResource(IPaddleApiClient client) : IPaddleCustomersResource
{
    private const string BasePath = "customers";

    public Task<PaddleListEnvelope<PaddleCustomerDto>> ListAsync(PaddleCustomerListQuery? query = null, CancellationToken cancellationToken = default)
        => client.GetPageAsync<PaddleCustomerDto>(BasePath, query?.ToQuery(), cancellationToken);

    public IAsyncEnumerable<PaddleCustomerDto> ListAllAsync(PaddleCustomerListQuery? query = null, CancellationToken cancellationToken = default)
        => client.GetAllAsync<PaddleCustomerDto>(BasePath, query?.ToQuery(), cancellationToken);

    public Task<PaddleCustomerDto> GetAsync(string customerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        return client.GetAsync<PaddleCustomerDto>($"{BasePath}/{customerId}", query: null, cancellationToken);
    }

    public Task<PaddleCustomerDto> CreateAsync(PaddleCreateCustomerRequest request, string? idempotencyKey = null, CancellationToken cancellationToken = default)
        => client.PostAsync<PaddleCreateCustomerRequest, PaddleCustomerDto>(BasePath, request, idempotencyKey, cancellationToken);

    public Task<PaddleCustomerDto> UpdateAsync(string customerId, PaddleUpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        return client.PatchAsync<PaddleUpdateCustomerRequest, PaddleCustomerDto>($"{BasePath}/{customerId}", request, idempotencyKey: null, cancellationToken);
    }
}

public sealed class PaddleCustomerListQuery
{
    public string? Status { get; set; }
    public string? Email { get; set; }
    public string? Search { get; set; }
    public string? Id { get; set; }
    public string? After { get; set; }
    public int? PerPage { get; set; }
    public string? OrderBy { get; set; }

    public Dictionary<string, string?> ToQuery() => new()
    {
        ["status"] = Status,
        ["email"] = Email,
        ["search"] = Search,
        ["id"] = Id,
        ["after"] = After,
        ["per_page"] = PerPage?.ToString(),
        ["order_by"] = OrderBy,
    };
}
