using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Bogdabot.Shared.Admin;
using Microsoft.Extensions.DependencyInjection;

namespace Bogdabot.Server.Admin;

public sealed class AdminSocketService(
    IServiceScopeFactory serviceScopeFactory,
    AdminSocketOptions options,
    ILogger<AdminSocketService> logger) : BackgroundService
{
    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;
    private AdminSocketOptions Options { get; } = options;
    private ILogger<AdminSocketService> Logger { get; } = logger;
    private Socket? Listener { get; set; }

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        if (OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("The admin service requires Unix domain sockets.");
        }

        var directory = Path.GetDirectoryName(Options.SocketPath)
            ?? throw new ArgumentException("The admin socket path must include a directory.", nameof(Options));
        Directory.CreateDirectory(directory);

        if (File.Exists(Options.SocketPath))
        {
            throw new IOException($"An entry already exists at the admin socket path: {Options.SocketPath}");
        }

        var listener = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        var bound = false;
        try
        {
            listener.Bind(new UnixDomainSocketEndPoint(Options.SocketPath));
            bound = true;
            File.SetUnixFileMode(
                Options.SocketPath,
                UnixFileMode.UserRead | UnixFileMode.UserWrite);
            listener.Listen(backlog: 10);
            Listener = listener;
            return base.StartAsync(cancellationToken);
        }
        catch
        {
            listener.Dispose();
            if (bound)
            {
                File.Delete(Options.SocketPath);
            }

            throw;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var listener = Listener
            ?? throw new InvalidOperationException("The admin socket listener was not initialized.");
        try
        {
            Logger.LogInformation("Admin socket listening at {SocketPath}", Options.SocketPath);

            while (!stoppingToken.IsCancellationRequested)
            {
                using var client = await listener.AcceptAsync(stoppingToken);
                try
                {
                    await ProcessClientAsync(client, stoppingToken);
                }
                catch (IOException exception) when (!stoppingToken.IsCancellationRequested)
                {
                    Logger.LogWarning(exception, "An admin socket client disconnected during a request.");
                }
                catch (SocketException exception) when (!stoppingToken.IsCancellationRequested)
                {
                    Logger.LogWarning(exception, "An admin socket client encountered a socket error.");
                }
            }
        }
        finally
        {
            listener.Dispose();
            Listener = null;
            File.Delete(Options.SocketPath);
        }
    }

    private async Task ProcessClientAsync(Socket client, CancellationToken cancellationToken)
    {
        await using var stream = new NetworkStream(client, ownsSocket: false);
        using var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true);
        await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true)
        {
            AutoFlush = true
        };

        var line = await reader.ReadLineAsync(cancellationToken);
        if (line is null)
        {
            return;
        }

        AdminResponse response;
        AdminRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<AdminRequest>(line, AdminJson.Options);
        }
        catch (JsonException exception)
        {
            Logger.LogWarning(exception, "Received invalid JSON over the admin socket.");
            response = new AdminResponse(false, Error: "Invalid JSON admin request.");
            await WriteResponseAsync(writer, response, cancellationToken);
            return;
        }

        if (request is null)
        {
            response = new AdminResponse(false, Error: "Admin request cannot be null.");
        }
        else
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<IAdminCommandHandler>();
            response = await handler.ExecuteAsync(request, cancellationToken);
        }

        await WriteResponseAsync(writer, response, cancellationToken);
    }

    private static Task WriteResponseAsync(
        StreamWriter writer,
        AdminResponse response,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(response, AdminJson.Options);
        return writer.WriteLineAsync(json.AsMemory(), cancellationToken);
    }
}
