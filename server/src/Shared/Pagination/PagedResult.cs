namespace Bogdabot.Server.Shared.Pagination;

/// <summary>
/// A single page of results. <see cref="HasNextPage" /> tells whether requesting the next page would yield more items.
/// </summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    bool HasNextPage
)
{
    public static PagedResult<T> Empty(PageRequest request) => new([], request.Page, request.PageSize, false);

    public PagedResult<TResult> Map<TResult>(Func<T, TResult> selector)
        => new(Items.Select(selector).ToList(), Page, PageSize, HasNextPage);
}
