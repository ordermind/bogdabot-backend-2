using Bogdabot.Server.Shared.Pagination;
using Bogdabot.Server.Shared.ValueObjects;
using Bogdabot.Server.Trading.Entities;

namespace Bogdabot.Server.Shared.Trading;

public interface IStoppedTraderRepository
{
    Task<PagedResult<StoppedTraderEntity>> GetAllForUser(ulong userId, PageRequest page);
    Task<StoppedTraderEntity?> GetOneByUserIdAndGuid(ulong userId, NonEmptyGuid traderGuid);
    Task Delete(StoppedTraderEntity stoppedTrader);
}