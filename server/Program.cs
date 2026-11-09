using Bogdabot.Server.Account.Bootstrap;
using Bogdabot.Server.Admin.Bootstrap;
using Bogdabot.Server.Infrastructure;
using Bogdabot.Server.Shared.Bootstrap;
using Bogdabot.Server.Trading.Bootstrap;
using Bogdabot.Server.Web.Bootstrap;

namespace Bogdabot.Server;

class Program
{
    private static void AddServiceLoaders(WebApplicationBuilder builder)
    {
        var sharedServiceLoader = new SharedServiceLoader(builder);
        sharedServiceLoader.AddServices();

        var infrastructureServiceLoader = new InfrastructureServiceLoader(builder, sharedServiceLoader.EnvironmentVariableService!);
        infrastructureServiceLoader.AddServices();

        var accountServiceLoader = new AccountServiceLoader(builder);
        accountServiceLoader.AddServices();

        var tradingServiceLoader = new TradingServiceLoader(builder);
        tradingServiceLoader.AddServices();

        var adminServiceLoader = new AdminServiceLoader(builder);
        adminServiceLoader.AddServices();

        var webServiceLoader = new WebServiceLoader(builder);
        webServiceLoader.AddServices();
    }

    private static async Task RunBootstrappers(WebApplication app)
    {
        var sharedBootstrapper = new SharedBootstrapper();
        await sharedBootstrapper.Run();

        var infrastructureBootstrapper = new InfrastructureBootstrapper(app);
        await infrastructureBootstrapper.Run();

        var accountBootstrapper = new AccountBootstrapper(app);
        await accountBootstrapper.Run();

        var tradingBootstrapper = new TradingBootstrapper();
        await tradingBootstrapper.Run();

        var webBootstrapper = new WebBootstrapper(app);
        await webBootstrapper.Run();
    }

    static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.
        AddServiceLoaders(builder);

        var app = builder.Build();

        await RunBootstrappers(app);

        await app.RunAsync();
    }

}
