using Bogdabot.Server.Shared.Actors;
using Bogdabot.Server.Shared.Database;
using Bogdabot.Shared.Admin;

namespace Bogdabot.Server.Admin;

public sealed class AdminCommandHandler(
    IMainDbMigrationService migrationService,
    IAkkaHost akkaHost,
    ITraderDatabaseManager traderDatabaseManager,
    ILogger<AdminCommandHandler> logger) : IAdminCommandHandler
{
    private IMainDbMigrationService MigrationService { get; } = migrationService;
    private IAkkaHost AkkaHost { get; } = akkaHost;
    private ITraderDatabaseManager TraderDatabaseManager { get; } = traderDatabaseManager;
    private ILogger<AdminCommandHandler> Logger { get; } = logger;

    public async Task<AdminResponse> ExecuteAsync(AdminRequest request, CancellationToken cancellationToken)
    {
        return request.Command switch
        {
            AdminCommands.MigrateFreshMain => await MigrateFreshMainAsync(cancellationToken),
            AdminCommands.MigrateFreshTraders => await MigrateFreshTradersAsync(cancellationToken),
            _ => new AdminResponse(false, Error: $"Unsupported admin command: {request.Command}")
        };
    }

    private async Task<AdminResponse> MigrateFreshTradersAsync(CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                await AkkaHost.StopAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                TraderDatabaseManager.DeleteAllDatabases();
            }
            finally
            {
                await AkkaHost.StartAsync(CancellationToken.None);
            }

            return new AdminResponse(
                true,
                Message: "All trader databases were successfully reset and migrations were run.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to reset trader databases.");
            return new AdminResponse(
                false,
                Error: "An unexpected error occurred while resetting the trader databases.");
        }
    }

    private async Task<AdminResponse> MigrateFreshMainAsync(CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                await AkkaHost.StopAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
                TraderDatabaseManager.DeleteAllDatabases();
                await MigrationService.MigrateFresh();
            }
            finally
            {
                await AkkaHost.StartAsync(CancellationToken.None);
            }

            return new AdminResponse(
                true,
                Message: "The database was successfully reset and migrations were run.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Failed to reset the main database.");
            return new AdminResponse(
                false,
                Error: "An unexpected error occurred while resetting the database.");
        }
    }
}
