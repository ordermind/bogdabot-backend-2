namespace Bogdabot.Server.Shared.Pagination;

public static class Paginator
{
    /// <summary>
    /// Processes every item as pages are fetched, retaining at most one page in memory.
    /// </summary>
    public static async Task ForEachAsync<T>(
        Func<PageRequest, Task<PagedResult<T>>> fetchPage,
        Func<T, Task> processItem,
        int pageSize = PageRequest.MaxPageSize
    )
    {
        var request = PageRequest.First(pageSize);

        while (true)
        {
            var page = await fetchPage(request);
            foreach (var item in page.Items)
            {
                await processItem(item);
            }

            if (!page.HasNextPage)
            {
                return;
            }

            request = request.Next();
        }
    }

    /// <summary>
    /// Collects every item by requesting consecutive pages until no page is left. Each individual request is still
    /// bounded by <see cref="PageRequest.MaxPageSize" />. All pages are fetched before returning, so callers that act on
    /// the results (and may thereby change the underlying data) do not shift the offsets of pages still to be read.
    /// </summary>
    public static async Task<IReadOnlyList<T>> FetchAll<T>(
        Func<PageRequest, Task<PagedResult<T>>> fetchPage,
        int pageSize = PageRequest.MaxPageSize
    )
    {
        var items = new List<T>();
        var request = PageRequest.First(pageSize);

        while (true)
        {
            var page = await fetchPage(request);
            items.AddRange(page.Items);

            if (!page.HasNextPage)
            {
                return items;
            }

            request = request.Next();
        }
    }
}
