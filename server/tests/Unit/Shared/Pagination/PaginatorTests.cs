using Bogdabot.Server.Shared.Pagination;

namespace Bogdabot.Server.Tests.Unit.Shared.Pagination;

public class PaginatorTests
{
    [Fact]
    public async Task FetchAll_RequestsConsecutivePagesUntilNoNextPage()
    {
        var requests = new List<PageRequest>();

        var items = await Paginator.FetchAll(request =>
        {
            requests.Add(request);
            var hasNextPage = request.Page < 3;

            return Task.FromResult(new PagedResult<int>([request.Page * 10, request.Page * 10 + 1], request.Page, request.PageSize, hasNextPage));
        }, pageSize: 2);

        Assert.Equal([10, 11, 20, 21, 30, 31], items);
        Assert.Equal([new PageRequest(1, 2), new PageRequest(2, 2), new PageRequest(3, 2)], requests);
    }

    [Fact]
    public async Task FetchAll_StopsAfterFirstPage_WhenThereIsNoNextPage()
    {
        var calls = 0;

        var items = await Paginator.FetchAll(request =>
        {
            calls++;
            return Task.FromResult(PagedResult<int>.Empty(request));
        });

        Assert.Empty(items);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task FetchAll_DefaultsToMaxPageSize()
    {
        PageRequest? received = null;

        await Paginator.FetchAll(request =>
        {
            received = request;
            return Task.FromResult(PagedResult<int>.Empty(request));
        });

        Assert.Equal(PageRequest.MaxPageSize, received?.PageSize);
    }

    [Fact]
    public async Task FetchAll_PropagatesFailures()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            Paginator.FetchAll<int>(_ => Task.FromException<PagedResult<int>>(new InvalidOperationException("read failed"))));
    }
}
