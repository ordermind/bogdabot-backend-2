using Bogdabot.Server.Shared.Pagination;
using Bogdabot.Server.Shared.ValueObjects;
using Bogdabot.Server.Trading.Entities;

namespace Bogdabot.Server.Shared.Trading;

public interface ITraderRepository
{
    Task<PagedResult<TraderEntity>> GetAllForUser(ulong userId, PageRequest page);

    Task<TraderEntity?> GetOneByUserIdAndGuid(ulong userId, NonEmptyGuid traderGuid);
}