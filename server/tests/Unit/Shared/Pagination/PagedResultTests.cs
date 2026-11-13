using Bogdabot.Server.Shared.Pagination;

namespace Bogdabot.Server.Tests.Unit.Shared.Pagination;

public class PagedResultTests
{
    [Fact]
    public void Empty_HasNoItemsAndNoNextPage()
    {
        var result = PagedResult<int>.Empty(new PageRequest(4, 10));

        Assert.Empty(result.Items);
        Assert.Equal(4, result.Page);
        Assert.Equal(10, result.PageSize);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void Map_TransformsItemsAndKeepsPageMetadata()
    {
        var result = new PagedResult<int>([1, 2], 2, 2, true).Map(item => item.ToString());

        Assert.Equal(["1", "2"], result.Items);
        Assert.Equal(2, result.Page);
        Assert.Equal(2, result.PageSize);
        Assert.True(result.HasNextPage);
    }
}
