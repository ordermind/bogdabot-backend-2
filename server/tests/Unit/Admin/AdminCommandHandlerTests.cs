using Bogdabot.Cli;
using Bogdabot.Server.Admin;
using Bogdabot.Server.Shared.Actors;
using Bogdabot.Server.Shared.Database;
using Bogdabot.Shared.Admin;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Bogdabot.Server.Tests.Unit.Admin;

public class AdminCommandHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_MigrateFreshMain_RunsStepsInOrderAndReturnsMessage()
    {
        var events = new List<string>();
        var migrationService = Substitute.For<IMainDbMigrationService>();
        migrationService.MigrateFresh().Returns(_ =>
        {
            events.Add("migrate");
            return Task.CompletedTask;
        });
        var akkaHost = CreateAkkaHost(events);
        var databaseManager = CreateDatabaseManager(events);
        var handler = CreateHandler(migrationService, akkaHost, databaseManager);

        var response = await handler.ExecuteAsync(
            new AdminRequest(AdminCommands.MigrateFreshMain),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Contains("successfully reset", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["stop", "delete", "migrate", "start"], events);
    }

    [Fact]
    public async Task ExecuteAsync_MigrateFreshTraders_RestartsAkkaAndReturnsError_WhenDeletingFails()
    {
        var events = new List<string>();
        var akkaHost = CreateAkkaHost(events);
        var databaseManager = Substitute.For<ITraderDatabaseManager>();
        databaseManager.When(manager => manager.DeleteAllDatabases())
            .Do(_ =>
            {
                events.Add("delete");
                throw new InvalidOperationException("delete failed");
            });
        var handler = CreateHandler(Substitute.For<IMainDbMigrationService>(), akkaHost, databaseManager);

        var response = await handler.ExecuteAsync(
            new AdminRequest(AdminCommands.MigrateFreshTraders),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("unexpected error", response.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["stop", "delete", "start"], events);
    }

    [Fact]
    public async Task ExecuteAsync_MigrateFreshTraders_RunsStepsInOrderAndReturnsMessage()
    {
        var events = new List<string>();
        var akkaHost = CreateAkkaHost(events);
        var databaseManager = CreateDatabaseManager(events);
        var handler = CreateHandler(Substitute.For<IMainDbMigrationService>(), akkaHost, databaseManager);

        var response = await handler.ExecuteAsync(
            new AdminRequest(AdminCommands.MigrateFreshTraders),
            CancellationToken.None);

        Assert.True(response.Success);
        Assert.Contains("successfully reset", response.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["stop", "delete", "start"], events);
    }

    [Fact]
    public async Task ExecuteAsync_MigrateFreshMain_RestartsAkkaAndReturnsError_WhenMigrationFails()
    {
        var events = new List<string>();
        var migrationService = Substitute.For<IMainDbMigrationService>();
        migrationService.MigrateFresh().Returns(_ =>
        {
            events.Add("migrate");
            throw new InvalidOperationException("migration failed");
        });
        var akkaHost = CreateAkkaHost(events);
        var databaseManager = CreateDatabaseManager(events);
        var handler = CreateHandler(migrationService, akkaHost, databaseManager);

        var response = await handler.ExecuteAsync(
            new AdminRequest(AdminCommands.MigrateFreshMain),
            CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("unexpected error", response.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(["stop", "delete", "migrate", "start"], events);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownCommand_ReturnsErrorWithoutCallingServices()
    {
        var migrationService = Substitute.For<IMainDbMigrationService>();
        var akkaHost = Substitute.For<IAkkaHost>();
        var databaseManager = Substitute.For<ITraderDatabaseManager>();
        var handler = CreateHandler(migrationService, akkaHost, databaseManager);

        var response = await handler.ExecuteAsync(new AdminRequest("unknown"), CancellationToken.None);

        Assert.False(response.Success);
        Assert.Contains("unknown", response.Error);
        await akkaHost.DidNotReceive().StopAsync(Arg.Any<CancellationToken>());
        databaseManager.DidNotReceive().DeleteAllDatabases();
        await migrationService.DidNotReceive().MigrateFresh();
    }

    [Fact]
    public async Task ExecuteAsync_PropagatesCancellationAndRestartsAkka()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var akkaHost = Substitute.For<IAkkaHost>();
        akkaHost.StopAsync(cancellation.Token)
            .Returns(_ => Task.FromCanceled(cancellation.Token));
        akkaHost.StartAsync(Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        var databaseManager = Substitute.For<ITraderDatabaseManager>();
        var migrationService = Substitute.For<IMainDbMigrationService>();
        var handler = CreateHandler(migrationService, akkaHost, databaseManager);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            handler.ExecuteAsync(new AdminRequest(AdminCommands.MigrateFreshMain), cancellation.Token));

        databaseManager.DidNotReceive().DeleteAllDatabases();
        await akkaHost.Received(1).StartAsync(CancellationToken.None);
    }

    private static IAkkaHost CreateAkkaHost(List<string> events)
    {
        var akkaHost = Substitute.For<IAkkaHost>();
        akkaHost.StopAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            events.Add("stop");
            return Task.CompletedTask;
        });
        akkaHost.StartAsync(Arg.Any<CancellationToken>()).Returns(_ =>
        {
            events.Add("start");
            return Task.CompletedTask;
        });
        return akkaHost;
    }

    private static ITraderDatabaseManager CreateDatabaseManager(List<string> events)
    {
        var databaseManager = Substitute.For<ITraderDatabaseManager>();
        databaseManager.When(manager => manager.DeleteAllDatabases())
            .Do(_ => events.Add("delete"));
        return databaseManager;
    }

    private static AdminCommandHandler CreateHandler(
        IMainDbMigrationService migrationService,
        IAkkaHost akkaHost,
        ITraderDatabaseManager databaseManager) =>
        new(migrationService, akkaHost, databaseManager, NullLogger<AdminCommandHandler>.Instance);
}
