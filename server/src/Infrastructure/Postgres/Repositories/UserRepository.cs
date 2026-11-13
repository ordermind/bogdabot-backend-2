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
        var users = new List<UserEntity>(page.PageSize + 1);
        var skippedUsers = 0;
        var sourcePage = 1;

        while (users.Count <= page.PageSize)
        {
            var activeUserIds = await traderContext.Traders
                .Where(trader => trader.Status == TraderStatusEnum.Running)
                .Select(trader => trader.UserId)
                .Distinct()
                .OrderBy(userId => userId)
                .ToPagedResultAsync(new PageRequest(sourcePage, page.PageSize));

            if (activeUserIds.Items.Count > 0)
            {
                var existingUsers = await userContext.Users
                    .Where(user => activeUserIds.Items.Contains(user.Id))
                    .OrderBy(user => user.Id)
                    .ToListAsync();

                foreach (var user in existingUsers)
                {
                    if (skippedUsers < page.Offset)
                    {
                        skippedUsers++;
                        continue;
                    }

                    users.Add(user);
                    if (users.Count > page.PageSize)
                    {
                        break;
                    }
                }
            }

            if (users.Count > page.PageSize || !activeUserIds.HasNextPage)
            {
                break;
            }

            sourcePage++;
        }

        var hasNextPage = users.Count > page.PageSize;
        if (hasNextPage)
        {
            users.RemoveAt(users.Count - 1);
        }

        return new PagedResult<UserEntity>(users, page.Page, page.PageSize, hasNextPage);
    }
}
