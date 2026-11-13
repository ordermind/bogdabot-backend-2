using Bogdabot.Server.Shared.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Bogdabot.Server.Infrastructure.Miscellaneous.Database.Extensions;

public static class QueryablePaginationExtensions
{
    /// <summary>
    /// Executes the query for a single page. The query must be deterministically ordered, otherwise rows may be skipped
    /// or repeated across pages. One extra row is requested to find out whether a next page exists without a count query.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IOrderedQueryable<T> query,
        PageRequest request,
        CancellationToken cancellationToken = default
    )
    {
        var items = await query
            .Skip(request.Offset)
            .Take(request.PageSize + 1)
            .ToListAsync(cancellationToken);

        var hasNextPage = items.Count > request.PageSize;
        if (hasNextPage)
        {
            items.RemoveAt(items.Count - 1);
        }

        return new PagedResult<T>(items, request.Page, request.PageSize, hasNextPage);
    }
}
