namespace Odin.Api.Pagination;

/// <summary>
/// Standard request shape for paged list endpoints. <c>Take</c> is hard-clamped at 100 in
/// <see cref="Sanitized"/> so callers can't request unbounded pages; <c>Skip</c> floors to 0.
/// <c>Search</c> is free-form and interpreted per-endpoint; <c>Sort</c> is a comma-separated
/// list of <c>field</c> or <c>-field</c> (descending) tokens.
/// </summary>
public sealed record PageRequest(int Skip = 0, int Take = 25, string? Search = null, string? Sort = null)
{
    public const int MaxTake = 100;

    public PageRequest Sanitized() => this with
    {
        Skip = Math.Max(0, Skip),
        Take = Math.Clamp(Take, 1, MaxTake),
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
        Sort = string.IsNullOrWhiteSpace(Sort) ? null : Sort.Trim(),
    };
}

/// <summary>
/// Standard response shape for paged list endpoints. <c>TotalCount</c> is the server-side
/// total (unfiltered by paging) so the FE can render correct pagination controls.
/// </summary>
public sealed record PageResponse<T>(IReadOnlyList<T> Items, int TotalCount, int Skip, int Take)
{
    public static PageResponse<T> Empty(PageRequest request) =>
        new([], 0, request.Skip, request.Take);
}
