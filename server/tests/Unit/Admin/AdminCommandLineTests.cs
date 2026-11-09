using Bogdabot.Cli;
using Bogdabot.Shared.Admin;

namespace Bogdabot.Server.Tests.Unit.Admin;

public class AdminCommandLineTests
{
    [Fact]
    public async Task RunAsync_HelpAndUnknownCommands_DoNotContactServer()
    {
        var client = new AdminClient(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var commandLine = new AdminCommandLine(client);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var helpResult = await commandLine.RunAsync(["--help"], output, error, CancellationToken.None);
        var unknownResult = await commandLine.RunAsync(["unknown"], output, error, CancellationToken.None);

        Assert.Equal(0, helpResult);
        Assert.Equal(2, unknownResult);
        Assert.Contains("Usage:", output.ToString());
        Assert.Contains("Usage:", error.ToString());
    }

    [Fact]
    public async Task RunAsync_ReportsConnectionFailure()
    {
        var client = new AdminClient(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var commandLine = new AdminCommandLine(client);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var result = await commandLine.RunAsync(
            [AdminCommands.MigrateFreshMain],
            output,
            error,
            CancellationToken.None);

        Assert.Equal(1, result);
        Assert.Contains("Unable to communicate", error.ToString());
    }

    [Fact]
    public async Task RunAsync_ReportsCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var client = new AdminClient(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var commandLine = new AdminCommandLine(client);
        using var output = new StringWriter();
        using var error = new StringWriter();

        var result = await commandLine.RunAsync(
            [AdminCommands.MigrateFreshMain],
            output,
            error,
            cancellation.Token);

        Assert.Equal(130, result);
        Assert.Contains("cancelled", error.ToString(), StringComparison.OrdinalIgnoreCase);
    }
}
