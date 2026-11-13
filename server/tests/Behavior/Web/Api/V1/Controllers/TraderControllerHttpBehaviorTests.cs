using System.Net;
using System.Net.Http.Json;
using Bogdabot.Server.Account.Entities;
using Bogdabot.Server.Shared.Pagination;
using Bogdabot.Server.Shared.Trading;
using Bogdabot.Server.Shared.Users;
using Bogdabot.Server.Shared.ValueObjects;
using Bogdabot.Server.Trading.Entities;
using Bogdabot.Server.Trading.Enums;
using Bogdabot.Server.Web.Api.V1.Transformers;
using Bogdabot.Server.Web.Gateway;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;

namespace Bogdabot.Server.Tests.Behavior.Web.Api.V1.Controllers;

public class TraderControllerHttpBehaviorTests
{
    [Fact]
    public async Task CreateTrader_WithInvalidPayload_ReturnsBadRequestValidationProblem()
    {
        var currentUserService = Substitute.For<IFetchesCurrentUser>();
        currentUserService.GetCurrentUser().Returns(Task.FromResult<UserEntity?>(CreateUser()));

        var traderGateway = Substitute.For<IWebTraderGateway>();
        var traderRepository = Substitute.For<ITraderRepository>();
        var stoppedTraderRepository = Substitute.For<IStoppedTraderRepository>();

        using var host = await CreateHost(currentUserService, traderGateway, traderRepository, stoppedTraderRepository);
        using var client = host.GetTestClient();

        var response = await client.PostAsJsonAsync("/api/v1/traders/create", new { }, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(payload);
    }

    [Fact]
    public async Task StopTrader_WithValidGuid_ReturnsOkAndDispatchesGatewayRequest()
    {
        var currentUser = CreateUser();
        var traderGuid = Guid.NewGuid();
        var validatedGuid = NonEmptyGuid.FromValue(traderGuid);

        var currentUserService = Substitute.For<IFetchesCurrentUser>();
        currentUserService.GetCurrentUser().Returns(Task.FromResult<UserEntity?>(currentUser));

        var traderGateway = Substitute.For<IWebTraderGateway>();
        traderGateway.StopOneTrader(Arg.Any<TraderIdentifier>())
            .Returns(Task.FromResult<Result<NonEmptyGuid>>(new Result<NonEmptyGuid>.Ok(validatedGuid)));

        var traderRepository = Substitute.For<ITraderRepository>();
        traderRepository.GetOneByUserIdAndGuid(currentUser.Id, validatedGuid)
            .Returns(Task.FromResult<TraderEntity?>(CreateTrader(currentUser.Id, currentUser.Guid, validatedGuid)));
        var stoppedTraderRepository = Substitute.For<IStoppedTraderRepository>();

        using var host = await CreateHost(currentUserService, traderGateway, traderRepository, stoppedTraderRepository);
        using var client = host.GetTestClient();

        var response = await client.PostAsync($"/api/v1/traders/{traderGuid}/stop", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await traderGateway.Received(1).StopOneTrader(Arg.Any<TraderIdentifier>());
    }

    [Fact]
    public async Task StartTrader_WithUnknownTrader_ReturnsNotFound()
    {
        var currentUser = CreateUser();

        var currentUserService = Substitute.For<IFetchesCurrentUser>();
        currentUserService.GetCurrentUser().Returns(Task.FromResult<UserEntity?>(currentUser));

        var traderGateway = Substitute.For<IWebTraderGateway>();
        var traderRepository = Substitute.For<ITraderRepository>();
        traderRepository.GetOneByUserIdAndGuid(Arg.Any<ulong>(), Arg.Any<NonEmptyGuid>())
            .Returns(Task.FromResult<TraderEntity?>(null));
        var stoppedTraderRepository = Substitute.For<IStoppedTraderRepository>();

        using var host = await CreateHost(currentUserService, traderGateway, traderRepository, stoppedTraderRepository);
        using var client = host.GetTestClient();

        var response = await client.PostAsync($"/api/v1/traders/{Guid.NewGuid()}/start", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await traderGateway.DidNotReceive().StartActiveTrader(Arg.Any<TraderIdentifier>());
    }

    [Fact]
    public async Task StartTrader_WithStoppedTrader_ReturnsBadRequestWithErrorMessage()
    {
        var currentUser = CreateUser();
        var traderGuid = NonEmptyGuid.NewGuid();

        var currentUserService = Substitute.For<IFetchesCurrentUser>();
        currentUserService.GetCurrentUser().Returns(Task.FromResult<UserEntity?>(currentUser));

        var traderGateway = Substitute.For<IWebTraderGateway>();
        var traderRepository = Substitute.For<ITraderRepository>();
        var stoppedTraderRepository = Substitute.For<IStoppedTraderRepository>();
        stoppedTraderRepository.GetOneByUserIdAndGuid(currentUser.Id, traderGuid)
            .Returns(Task.FromResult<StoppedTraderEntity?>(CreateStoppedTrader(currentUser.Id, currentUser.Guid, traderGuid)));

        using var host = await CreateHost(currentUserService, traderGateway, traderRepository, stoppedTraderRepository);
        using var client = host.GetTestClient();

        var response = await client.PostAsync($"/api/v1/traders/{traderGuid}/start", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ProblemDetails>(TestContext.Current.CancellationToken);
        Assert.NotNull(payload);
        Assert.Equal($"The stopped trader {traderGuid} could not be restarted. Please create a new trader instead.", payload.Title);
        await traderGateway.DidNotReceive().StartActiveTrader(Arg.Any<TraderIdentifier>());
    }

    [Theory]
    [InlineData("/api/v1/traders?pageSize=101")]
    [InlineData("/api/v1/traders?pageSize=0")]
    [InlineData("/api/v1/traders?page=0")]
    [InlineData("/api/v1/stopped-traders?pageSize=101")]
    [InlineData("/api/v1/stopped-traders?pageSize=0")]
    [InlineData("/api/v1/stopped-traders?page=0")]
    public async Task GetAll_WithOutOfRangePaging_ReturnsBadRequestWithoutQueryingRepository(string url)
    {
        var currentUserService = Substitute.For<IFetchesCurrentUser>();
        currentUserService.GetCurrentUser().Returns(Task.FromResult<UserEntity?>(CreateUser()));

        var traderRepository = Substitute.For<ITraderRepository>();
        var stoppedTraderRepository = Substitute.For<IStoppedTraderRepository>();

        using var host = await CreateHost(currentUserService, Substitute.For<IWebTraderGateway>(), traderRepository, stoppedTraderRepository);
        using var client = host.GetTestClient();

        var response = await client.GetAsync(url, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        await traderRepository.DidNotReceiveWithAnyArgs().GetAllForUser(default, default!);
        await stoppedTraderRepository.DidNotReceiveWithAnyArgs().GetAllForUser(default, default!);
    }

    [Fact]
    public async Task GetAllTraders_WithPagingQuery_ForwardsPageAndReturnsPageEnvelope()
    {
        var currentUser = CreateUser();
        var currentUserService = Substitute.For<IFetchesCurrentUser>();
        currentUserService.GetCurrentUser().Returns(Task.FromResult<UserEntity?>(currentUser));

        var traderRepository = Substitute.For<ITraderRepository>();
        traderRepository.GetAllForUser(currentUser.Id, new PageRequest(2, 100))
            .Returns(Task.FromResult(new PagedResult<TraderEntity>([], 2, 100, true)));

        using var host = await CreateHost(currentUserService, Substitute.For<IWebTraderGateway>(), traderRepository, Substitute.For<IStoppedTraderRepository>());
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/api/v1/traders?page=2&pageSize=100", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var payload = System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, payload.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(2, payload.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(100, payload.RootElement.GetProperty("pageSize").GetInt32());
        Assert.True(payload.RootElement.GetProperty("hasNextPage").GetBoolean());
        await traderRepository.Received(1).GetAllForUser(currentUser.Id, new PageRequest(2, 100));
    }

    [Fact]
    public async Task GetAllStoppedTraders_WithoutPagingQuery_RequestsFirstPageWithDefaultSize()
    {
        var currentUser = CreateUser();
        var currentUserService = Substitute.For<IFetchesCurrentUser>();
        currentUserService.GetCurrentUser().Returns(Task.FromResult<UserEntity?>(currentUser));

        var stoppedTraderRepository = Substitute.For<IStoppedTraderRepository>();
        stoppedTraderRepository.GetAllForUser(Arg.Any<ulong>(), Arg.Any<PageRequest>())
            .Returns(call => Task.FromResult(PagedResult<StoppedTraderEntity>.Empty(call.Arg<PageRequest>()!)));

        using var host = await CreateHost(currentUserService, Substitute.For<IWebTraderGateway>(), Substitute.For<ITraderRepository>(), stoppedTraderRepository);
        using var client = host.GetTestClient();

        var response = await client.GetAsync("/api/v1/stopped-traders", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await stoppedTraderRepository.Received(1).GetAllForUser(currentUser.Id, new PageRequest(1, PageRequest.DefaultPageSize));
    }

    private static UserEntity CreateUser()
    {
        return new UserEntity
        {
            Id = 1,
            Guid = NonEmptyGuid.NewGuid(),
            UserName = "user@example.com",
            NormalizedUserName = "USER@EXAMPLE.COM",
            Email = "user@example.com",
            NormalizedEmail = "USER@EXAMPLE.COM"
        };
    }

    private static TraderEntity CreateTrader(ulong userId, NonEmptyGuid userGuid, NonEmptyGuid traderGuid)
    {
        var now = DateTimeOffset.UtcNow;
        return new TraderEntity
        {
            Guid = traderGuid,
            UserId = userId,
            UserGuid = userGuid,
            BrokerName = BrokerEnum.IBKR,
            StrategyName = TradingStrategyEnum.SpotGrid,
            BaseCurrency = new CurrencySymbol("BTC"),
            InitialPrice = new NonNegativeDecimalNumber(10m),
            InitialBaseBalance = new NonNegativeDecimalNumber(10m),
            QuoteCurrency = new CurrencySymbol("EUR"),
            InitialQuoteBalance = new NonNegativeDecimalNumber(5m),
            InitialTotalValue = new NonNegativeDecimalNumber(15m),
            FirstStartedAt = now,
            LastStartedAt = now,
            Status = TraderStatusEnum.Running
        };
    }

    private static StoppedTraderEntity CreateStoppedTrader(ulong userId, NonEmptyGuid userGuid, NonEmptyGuid traderGuid)
    {
        var now = DateTimeOffset.UtcNow;
        return new StoppedTraderEntity
        {
            Guid = traderGuid,
            UserId = userId,
            UserGuid = userGuid,
            BrokerName = BrokerEnum.IBKR,
            StrategyName = TradingStrategyEnum.SpotGrid,
            BaseCurrency = new CurrencySymbol("BTC"),
            InitialPrice = new NonNegativeDecimalNumber(10m),
            InitialBaseBalance = new NonNegativeDecimalNumber(10m),
            FinalPrice = new NonNegativeDecimalNumber(9m),
            FinalBaseBalance = new NonNegativeDecimalNumber(9m),
            QuoteCurrency = new CurrencySymbol("EUR"),
            InitialQuoteBalance = new NonNegativeDecimalNumber(5m),
            FinalQuoteBalance = new NonNegativeDecimalNumber(4m),
            InitialTotalValue = new NonNegativeDecimalNumber(15m),
            FinalTotalValue = new NonNegativeDecimalNumber(13m),
            UnrealizedResult = 0m,
            RealizedResult = 0m,
            TotalResult = 0m,
            UnrealizedProfitMargin = 0m,
            RealizedProfitMargin = 0m,
            TotalProfitMargin = 0m,
            StartedAt = now,
            StoppedAt = now
        };
    }

    private static async Task<IHost> CreateHost(
        IFetchesCurrentUser currentUserService,
        IWebTraderGateway traderGateway,
        ITraderRepository traderRepository,
        IStoppedTraderRepository stoppedTraderRepository)
    {
        return await new HostBuilder()
            .ConfigureWebHost(webHost =>
            {
                webHost.UseTestServer();
                webHost.ConfigureServices(services =>
                {
                    services.AddControllers()
                        .AddApplicationPart(typeof(Bogdabot.Server.Web.Api.V1.Controllers.TraderController).Assembly);

                    services.AddSingleton<WebTransformer>();
                    services.AddSingleton(currentUserService);
                    services.AddSingleton(traderGateway);
                    services.AddSingleton(traderRepository);
                    services.AddSingleton(stoppedTraderRepository);
                });
                webHost.Configure(app =>
                {
                    app.UseRouting();
                    app.UseEndpoints(endpoints => endpoints.MapControllers());
                });
            })
            .StartAsync(TestContext.Current.CancellationToken);
    }
}
