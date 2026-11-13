using Bogdabot.Server.Shared.Pagination;

namespace Bogdabot.Server.Tests.Unit.Shared.Pagination;

public class PageRequestTests
{
    [Fact]
    public void Defaults_ToFirstPageWithMaxPageSize()
    {
        var request = new PageRequest();

        Assert.Equal(1, request.Page);
        Assert.Equal(100, request.PageSize);
        Assert.Equal(PageRequest.MaxPageSize, request.PageSize);
        Assert.Equal(0, request.Offset);
    }

    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(2, 25, 25)]
    [InlineData(5, 100, 400)]
    public void Offset_SkipsAllPreviousPages(int page, int pageSize, int expectedOffset)
    {
        Assert.Equal(expectedOffset, new PageRequest(page, pageSize).Offset);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public void Constructor_RejectsPageSizeOutsideOneToMax(int pageSize)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new PageRequest(1, pageSize));

        Assert.Equal("pageSize", exception.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(PageRequest.MaxPage + 1)]
    public void Constructor_RejectsPageOutsideOneToMaxPage(int page)
    {
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() => new PageRequest(page, 10));

        Assert.Equal("page", exception.ParamName);
    }

    [Fact]
    public void Offset_DoesNotOverflow_ForHighestPageAndPageSize()
    {
        var request = new PageRequest(PageRequest.MaxPage, PageRequest.MaxPageSize);

        Assert.True(request.Offset >= 0);
        Assert.True(request.Offset + request.PageSize + 1 > request.Offset);
    }

    [Fact]
    public void Next_AdvancesPageAndKeepsPageSize()
    {
        var next = new PageRequest(3, 20).Next();

        Assert.Equal(new PageRequest(4, 20), next);
    }

    [Fact]
    public void First_UsesGivenPageSize()
    {
        Assert.Equal(new PageRequest(1, 10), PageRequest.First(10));
        Assert.Equal(new PageRequest(), PageRequest.First());
    }
}
