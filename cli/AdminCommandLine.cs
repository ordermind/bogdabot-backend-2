using System.Net.Sockets;
using System.Text.Json;
using Bogdabot.Shared.Admin;

namespace Bogdabot.Cli;

public sealed class AdminCommandLine(AdminClient client)
{
    private AdminClient Client { get; } = client;

    public async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        CancellationToken cancellationToken)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            await output.WriteLineAsync(Usage);
            return 0;
        }

        if (args.Length != 1 || !TryGetCommand(args[0], out var command))
        {
            await error.WriteLineAsync(Usage);
            return 2;
        }

        try
        {
            var response = await Client.SendAsync(new AdminRequest(command), cancellationToken);
            if (!response.Success)
            {
                await error.WriteLineAsync(response.Error ?? "The admin command failed.");
                return 1;
            }

            await output.WriteLineAsync(response.Message ?? "The admin command completed successfully.");
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await error.WriteLineAsync("The admin command was cancelled.");
            return 130;
        }
        catch (Exception exception) when (exception is SocketException or IOException or JsonException)
        {
            await error.WriteLineAsync($"Unable to communicate with the admin server: {exception.Message}");
            return 1;
        }
    }

    private const string Usage =
        "Usage: bogdabot <migrate-fresh-main|migrate-fresh-traders>";

    private static bool TryGetCommand(string argument, out string command)
    {
        if (argument is AdminCommands.MigrateFreshMain or AdminCommands.MigrateFreshTraders)
        {
            command = argument;
            return true;
        }

        command = string.Empty;
        return false;
    }
}
