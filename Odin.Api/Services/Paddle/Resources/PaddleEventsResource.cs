using Odin.Api.Services.Paddle.Models.Common;
using Odin.Api.Services.Paddle.Models.Notifications;

namespace Odin.Api.Services.Paddle.Resources;

public interface IPaddleEventsResource
{
    Task<PaddleListEnvelope<PaddleEventDto>> ListAsync(PaddleEventListQuery? query = null, CancellationToken cancellationToken = default);
    IAsyncEnumerable<PaddleEventDto> ListAllAsync(PaddleEventListQuery? query = null, CancellationToken cancellationToken = default);
}

public sealed class PaddleEventsResource(IPaddleApiClient client) : IPaddleEventsResource
{
    private const string BasePath = "events";

    public Task<PaddleListEnvelope<PaddleEventDto>> ListAsync(PaddleEventListQuery? query = null, CancellationToken cancellationToken = default)
        => client.GetPageAsync<PaddleEventDto>(BasePath, query?.ToQuery(), cancellationToken);

    public IAsyncEnumerable<PaddleEventDto> ListAllAsync(PaddleEventListQuery? query = null, CancellationToken cancellationToken = default)
        => client.GetAllAsync<PaddleEventDto>(BasePath, query?.ToQuery(), cancellationToken);
}

public sealed class PaddleEventListQuery
{
    public string? After { get; set; }
    public int? PerPage { get; set; }
    public string? OrderBy { get; set; }

    public Dictionary<string, string?> ToQuery() => new()
    {
        ["after"] = After,
        ["per_page"] = PerPage?.ToString(),
        ["order_by"] = OrderBy,
    };
}
