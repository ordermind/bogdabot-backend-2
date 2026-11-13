using Bogdabot.Server.Shared.Pagination;

namespace Bogdabot.Server.Tests.TestSupport;

public static class TestPages
{
    /// <summary>
    /// A first page that contains every given item and reports that no further page exists.
    /// </summary>
    public static PagedResult<T> Single<T>(IEnumerable<T> items)
        => new(items.ToList(), 1, PageRequest.DefaultPageSize, false);
}
