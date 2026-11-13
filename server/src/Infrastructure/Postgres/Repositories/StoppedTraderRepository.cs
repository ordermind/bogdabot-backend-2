using Bogdabot.Server.Infrastructure.Postgres.DbContexts;
using Bogdabot.Server.Trading.Entities;
using Microsoft.EntityFrameworkCore;
using Bogdabot.Server.Infrastructure.Miscellaneous.Database.Extensions;
using Bogdabot.Server.Shared.Pagination;
using Bogdabot.Server.Shared.ValueObjects;
using SharedStoppedTraderRepository = Bogdabot.Server.Shared.Trading.IStoppedTraderRepository;
using TradingStoppedTraderRepository = Bogdabot.Server.Trading.Repositories.IStoppedTraderRepository;
using Bogdabot.Server.Account.Entities;

namespace Bogdabot.Server.Infrastructure.Postgres.Repositories;

public class StoppedTraderRepository(IDbContextFactory<StoppedTraderDbContext> contextFactory) : SharedStoppedTraderRepository, TradingStoppedTraderRepository
{
    private IDbContextFactory<StoppedTraderDbContext> ContextFactory { get; } = contextFactory;

    public async Task<PagedResult<StoppedTraderEntity>> GetAllForUser(UserEntity userEntity, PageRequest page)
    {
        return await GetAllForUser(userEntity.Id, page);
    }

    public async Task<PagedResult<StoppedTraderEntity>> GetAllForUser(ulong userId, PageRequest page)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        return await GetAllForUser(userId, page, context);
    }

    public async Task<PagedResult<StoppedTraderEntity>> GetAllForUser(ulong userId, PageRequest page, StoppedTraderDbContext context)
    {
        return await context.StoppedTraders
            .Where(
                stoppedTrader => stoppedTrader.UserId == userId
            )
            .OrderBy(stoppedTrader => stoppedTrader.Id)
            .ToPagedResultAsync(page);
    }

    public async Task<PagedResult<StoppedTraderEntity>> GetAllForUser(NonEmptyGuid userGuid, PageRequest page)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        return await GetAllForUser(userGuid, page, context);
    }

    public async Task<PagedResult<StoppedTraderEntity>> GetAllForUser(NonEmptyGuid userGuid, PageRequest page, StoppedTraderDbContext context)
    {
        return await context.StoppedTraders
            .Where(
                stoppedTrader => stoppedTrader.UserGuid == userGuid
            )
            .OrderBy(stoppedTrader => stoppedTrader.Id)
            .ToPagedResultAsync(page);
    }

    public async Task<StoppedTraderEntity?> GetOneByUserIdAndGuid(ulong userId, NonEmptyGuid traderGuid)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        return await GetOneByUserIdAndGuid(userId, traderGuid, context);
    }

    public async Task<StoppedTraderEntity?> GetOneByUserIdAndGuid(ulong userId, NonEmptyGuid traderGuid, StoppedTraderDbContext context)
    {
        return await context.StoppedTraders
            .FirstOrDefaultAsync(stoppedTrader =>
                stoppedTrader.UserId == userId
                && stoppedTrader.Guid == traderGuid
            );
    }

    public async Task<StoppedTraderEntity?> GetOneByGuid(NonEmptyGuid traderGuid)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        return await GetOneByGuid(traderGuid, context);
    }

    public async Task<StoppedTraderEntity?> GetOneByGuid(NonEmptyGuid traderGuid, StoppedTraderDbContext context)
    {
        return await context.StoppedTraders.FirstOrDefaultAsync(trader => trader.Guid == traderGuid);
    }

    public async Task<StoppedTraderEntity> SaveTraderAsStopped(
        TraderEntity trader,
        NonNegativeDecimalNumber finalPrice,
        NonNegativeDecimalNumber finalBaseBalance,
        NonNegativeDecimalNumber finalQuoteBalance,
        decimal realizedResult
    )
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        return await SaveTraderAsStopped(
            trader,
            finalPrice,
            finalBaseBalance,
            finalQuoteBalance,
            realizedResult,
            context
        );
    }

    public async Task<StoppedTraderEntity> SaveTraderAsStopped(
        TraderEntity trader,
        NonNegativeDecimalNumber finalPrice,
        NonNegativeDecimalNumber finalBaseBalance,
        NonNegativeDecimalNumber finalQuoteBalance,
        decimal realizedResult,
        StoppedTraderDbContext context
    )
    {
        var finalTotalValue = finalBaseBalance * finalPrice + finalQuoteBalance;
        var unrealizedResult = finalTotalValue - trader.InitialTotalValue;
        var unrealizedProfitMargin = unrealizedResult / trader.InitialTotalValue;
        var realizedProfitMargin = realizedResult / trader.InitialTotalValue;

        var stoppedTrader = new StoppedTraderEntity
        {
            Guid = NonEmptyGuid.NewGuid(),
            UserId = trader.UserId,
            UserGuid = trader.UserGuid,
            BrokerName = trader.BrokerName,
            StrategyName = trader.StrategyName,
            BaseCurrency = trader.BaseCurrency,
            InitialPrice = trader.InitialPrice,
            InitialBaseBalance = trader.InitialBaseBalance,
            FinalPrice = finalPrice,
            FinalBaseBalance = finalBaseBalance,
            QuoteCurrency = trader.QuoteCurrency,
            InitialQuoteBalance = trader.InitialQuoteBalance,
            FinalQuoteBalance = finalQuoteBalance,
            InitialTotalValue = trader.InitialTotalValue,
            FinalTotalValue = finalTotalValue,
            UnrealizedResult = unrealizedResult,
            RealizedResult = realizedResult,
            TotalResult = unrealizedResult + realizedResult,
            UnrealizedProfitMargin = unrealizedProfitMargin,
            RealizedProfitMargin = realizedProfitMargin,
            TotalProfitMargin = unrealizedProfitMargin + realizedProfitMargin,
            StartedAt = trader.FirstStartedAt,
            StoppedAt = DateTimeOffset.UtcNow,
        };

        context.StoppedTraders.Add(stoppedTrader);

        return await Save(stoppedTrader, context);
    }

    public async Task<StoppedTraderEntity> Save(StoppedTraderEntity stoppedTrader)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        return await Save(stoppedTrader, context);
    }


    public async Task<StoppedTraderEntity> Save(StoppedTraderEntity stoppedTrader, StoppedTraderDbContext context)
    {
        if (context.Entry(stoppedTrader).State == EntityState.Detached)
        {
            context.StoppedTraders.Update(stoppedTrader);
        }

        await context.SaveChangesAsync();

        return stoppedTrader;
    }

    public async Task DeleteByGuid(NonEmptyGuid traderGuid)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        await DeleteByGuid(traderGuid, context);
    }

    public async Task DeleteByGuid(NonEmptyGuid traderGuid, StoppedTraderDbContext context)
    {
        var stoppedTrader = context.StoppedTraders.SingleOrDefault(stoppedTrader => stoppedTrader.Guid == traderGuid);
        if (stoppedTrader == null)
        {
            return;
        }

        await Delete(stoppedTrader, context);
    }

    public async Task Delete(StoppedTraderEntity stoppedTrader)
    {
        await using var context = await ContextFactory.CreateDbContextAsync();

        await Delete(stoppedTrader, context);
    }

    public async Task Delete(StoppedTraderEntity stoppedTrader, StoppedTraderDbContext context)
    {
        context.Remove(stoppedTrader);

        await context.SaveChangesAsync();
    }
}
