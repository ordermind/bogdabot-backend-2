using Bogdabot.Server.Account.Entities;
using Bogdabot.Server.Infrastructure.Postgres.DbContexts;
using Bogdabot.Server.Shared.Pagination;

namespace Bogdabot.Server.Trading.Repositories;

public interface IUserRepository
{
    Task<PagedResult<UserEntity>> GetAllUsersWithActiveTraders(PageRequest page);
    Task<PagedResult<UserEntity>> GetAllUsersWithActiveTraders(PageRequest page, UserDbContext context, TraderDbContext traderContext);
}