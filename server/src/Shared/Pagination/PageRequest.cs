namespace Bogdabot.Server.Shared.Pagination;

/// <summary>
/// A validated, 1-based page request. No page can ever contain more than <see cref="MaxPageSize" /> items.
/// </summary>
public sealed record PageRequest
{
    public const int MaxPageSize = 100;

    public const int DefaultPageSize = MaxPageSize;

    /// <summary>
    /// The highest page number that can be requested with any page size without the offset overflowing.
    /// </summary>
    public const int MaxPage = int.MaxValue / MaxPageSize;

    public int Page { get; }

    public int PageSize { get; }

    /// <summary>
    /// The number of items to skip to reach this page.
    /// </summary>
    public int Offset => (Page - 1) * PageSize;

    public PageRequest(int page = 1, int pageSize = DefaultPageSize)
    {
        if (page < 1 || page > MaxPage)
        {
            throw new ArgumentOutOfRangeException(nameof(page), page, $"Page must be between 1 and {MaxPage}.");
        }

        if (pageSize < 1 || pageSize > MaxPageSize)
        {
            throw new ArgumentOutOfRangeException(nameof(pageSize), pageSize, $"Page size must be between 1 and {MaxPageSize}.");
        }

        Page = page;
        PageSize = pageSize;
    }

    public static PageRequest First(int pageSize = DefaultPageSize) => new(1, pageSize);

    public PageRequest Next() => new(Page + 1, PageSize);
}
