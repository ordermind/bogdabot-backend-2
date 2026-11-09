using System.Net.Sockets;
using System.Text;
using Bogdabot.Cli;
using Bogdabot.Server.Admin;
using Bogdabot.Shared.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Bogdabot.Server.Tests.Unit.Admin;

public class AdminSocketServiceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Service_ProcessesJsonCommandAndRemovesSocketOnStop(bool succeeds)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsFreeBSD())
        {
            return;
        }

        var socketPath = CreateSocketPath();
        var handler = Substitute.For<IAdminCommandHandler>();
        handler.ExecuteAsync(Arg.Any<AdminRequest>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var command = call.Arg<AdminRequest>()?.Command ?? "unknown";
                return Task.FromResult(succeeds
                    ? new AdminResponse(true, Message: $"Handled {command}")
                    : new AdminResponse(false, Error: $"Rejected {command}"));
            });
        await using var provider = CreateProvider(handler);
        var service = CreateService(provider, socketPath);

        try
        {
            await service.StartAsync(TestContext.Current.CancellationToken);
            using var output = new StringWriter();
            using var error = new StringWriter();
            var exitCode = await new AdminCommandLine(new AdminClient(socketPath)).RunAsync(
                [AdminCommands.MigrateFreshMain],
                output,
                error,
                CancellationToken.None);

            Assert.Equal(succeeds ? 0 : 1, exitCode);
            Assert.Equal(succeeds ? $"Handled {AdminCommands.MigrateFreshMain}" : string.Empty, output.ToString().Trim());
            Assert.Equal(succeeds ? string.Empty : $"Rejected {AdminCommands.MigrateFreshMain}", error.ToString().Trim());
            await handler.Received(1).ExecuteAsync(
                Arg.Is<AdminRequest>(request => request != null &&
                                                request.Command == AdminCommands.MigrateFreshMain),
                Arg.Any<CancellationToken>());
            Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(socketPath));
        }
        finally
        {
            await service.StopAsync(TestContext.Current.CancellationToken);
            CleanupSocketPath(socketPath);
        }

        Assert.False(File.Exists(socketPath));
    }

    [Theory]
    [InlineData("not-json", "Invalid JSON admin request.")]
    [InlineData("null", "Admin request cannot be null.")]
    public async Task Service_ReturnsFailureForInvalidJson(string request, string expectedError)
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsFreeBSD())
        {
            return;
        }

        var socketPath = CreateSocketPath();
        var handler = Substitute.For<IAdminCommandHandler>();
        await using var provider = CreateProvider(handler);
        var service = CreateService(provider, socketPath);

        try
        {
            await service.StartAsync(TestContext.Current.CancellationToken);
            var response = await SendRawRequestAsync(socketPath, request);

            Assert.False(response.Success);
            var error = Assert.IsType<string>(response.Error);
            Assert.Equal(expectedError, error);
            await handler.DidNotReceive().ExecuteAsync(
                Arg.Any<AdminRequest>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            await service.StopAsync(TestContext.Current.CancellationToken);
            CleanupSocketPath(socketPath);
        }
    }

    [Fact]
    public async Task Service_DoesNotReplaceAnExistingSocketPath()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsFreeBSD())
        {
            return;
        }

        var socketPath = CreateSocketPath();
        Directory.CreateDirectory(Path.GetDirectoryName(socketPath)!);
        await File.WriteAllTextAsync(socketPath, "existing", TestContext.Current.CancellationToken);
        await using var provider = CreateProvider(Substitute.For<IAdminCommandHandler>());
        var service = CreateService(provider, socketPath);

        try
        {
            await Assert.ThrowsAsync<IOException>(() =>
                service.StartAsync(TestContext.Current.CancellationToken));
            Assert.Equal(
                "existing",
                await File.ReadAllTextAsync(socketPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            await service.StopAsync(TestContext.Current.CancellationToken);
            CleanupSocketPath(socketPath);
        }
    }

    [Fact]
    public async Task Service_ContinuesAfterClientDisconnectsDuringRequest()
    {
        if (!OperatingSystem.IsLinux() && !OperatingSystem.IsMacOS() && !OperatingSystem.IsFreeBSD())
        {
            return;
        }

        var socketPath = CreateSocketPath();
        var handler = Substitute.For<IAdminCommandHandler>();
        handler.ExecuteAsync(Arg.Any<AdminRequest>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new AdminResponse(true, Message: "ready")));
        await using var provider = CreateProvider(handler);
        var service = CreateService(provider, socketPath);

        try
        {
            await service.StartAsync(TestContext.Current.CancellationToken);
            using (var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
            {
                await socket.ConnectAsync(
                    new UnixDomainSocketEndPoint(socketPath),
                    TestContext.Current.CancellationToken);
                await socket.SendAsync(
                    Encoding.UTF8.GetBytes("{"),
                    SocketFlags.None,
                    TestContext.Current.CancellationToken);
                socket.LingerState = new LingerOption(true, 0);
            }

            var response = await new AdminClient(socketPath).SendAsync(
                new AdminRequest(AdminCommands.MigrateFreshMain),
                TestContext.Current.CancellationToken);

            Assert.True(response.Success);
            Assert.Equal("ready", response.Message);
        }
        finally
        {
            await service.StopAsync(TestContext.Current.CancellationToken);
            CleanupSocketPath(socketPath);
        }
    }

    private static ServiceProvider CreateProvider(IAdminCommandHandler handler) =>
        new ServiceCollection()
            .AddScoped<IAdminCommandHandler>(_ => handler)
            .BuildServiceProvider();

    private static AdminSocketService CreateService(IServiceProvider provider, string socketPath) =>
        new(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new AdminSocketOptions(socketPath),
            NullLogger<AdminSocketService>.Instance);

    private static string CreateSocketPath() =>
        Path.Combine(Path.GetTempPath(), $"bogdabot-admin-tests-{Guid.NewGuid():N}", AdminSocket.FileName);

    private static void CleanupSocketPath(string socketPath)
    {
        var directory = Path.GetDirectoryName(socketPath)!;
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static async Task<AdminResponse> SendRawRequestAsync(string socketPath, string request)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(socketPath));
        await using var stream = new NetworkStream(socket, ownsSocket: false);
        await using var writer = new StreamWriter(stream, Encoding.UTF8, bufferSize: 1024, leaveOpen: true)
        {
            AutoFlush = true
        };
        using var reader = new StreamReader(
            stream,
            Encoding.UTF8,
            detectEncodingFromByteOrderMarks: false,
            bufferSize: 1024,
            leaveOpen: true);

        await writer.WriteLineAsync(request);
        var response = await reader.ReadLineAsync();
        return System.Text.Json.JsonSerializer.Deserialize<AdminResponse>(response!, AdminJson.Options)!;
    }
}
