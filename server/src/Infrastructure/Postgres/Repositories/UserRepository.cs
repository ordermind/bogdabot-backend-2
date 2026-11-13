using Bogdabot.Server.Account.Entities;
using Bogdabot.Server.Infrastructure.Postgres.DbContexts;
using Bogdabot.Server.Trading.Repositories;
using Bogdabot.Server.Trading.Entities;
using Microsoft.EntityFrameworkCore;
using Bogdabot.Server.Infrastructure.Miscellaneous.Database.Extensions;
using Bogdabot.Server.Shared.Pagination;

namespace Bogdabot.Server.Infrastructure.Postgres.Repositories;

public class UserRepository(IDbContextFactory<UserDbContext> userContextFactory, IDbContextFactory<TraderDbContext> traderContextFactory) : IUserRepository
{
    private IDbContextFactory<UserDbContext> UserContextFactory { get; } = userContextFactory;

    private IDbContextFactory<TraderDbContext> TraderContextFactory { get; } = traderContextFactory;

    public async Task<PagedResult<UserEntity>> GetAllUsersWithActiveTraders(PageRequest page)
    {
        await using var userContext = await UserContextFactory.CreateDbContextAsync();

        await using var traderContext = await TraderContextFactory.CreateDbContextAsync();

        return await GetAllUsersWithActiveTraders(page, userContext, traderContext);
    }

    public async Task<PagedResult<UserEntity>> GetAllUsersWithActiveTraders(PageRequest page, UserDbContext userContext, TraderDbContext traderContext)
    {
        // Paginate over the distinct user ids so that the follow-up user query is bounded by the page size as well.
        var activeUserIds = await traderContext.Traders
            .Where(trader => trader.Status == TraderStatusEnum.Running)
            .Select(trader => trader.UserId)
            .Distinct()
            .OrderBy(userId => userId)
            .ToPagedResultAsync(page);

        if (activeUserIds.Items.Count == 0)
        {
            return PagedResult<UserEntity>.Empty(page);
        }

        var users = await userContext.Users
            .Where(user => activeUserIds.Items.Contains(user.Id))
            .OrderBy(user => user.Id)
            .ToListAsync();

        // Users whose record is missing are skipped, so a page may contain fewer items than its size even if there is a next page.
        return new PagedResult<UserEntity>(users, page.Page, page.PageSize, activeUserIds.HasNextPage);
    }
}
