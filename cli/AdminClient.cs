using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Bogdabot.Shared.Admin;

namespace Bogdabot.Cli;

public sealed class AdminClient(string socketPath = AdminSocket.SocketPath)
{
    private string SocketPath { get; } = socketPath;

    public async Task<AdminResponse> SendAsync(AdminRequest request, CancellationToken cancellationToken)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(SocketPath), cancellationToken);

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

        var requestJson = JsonSerializer.Serialize(request, AdminJson.Options);
        await writer.WriteLineAsync(requestJson.AsMemory(), cancellationToken);

        var responseJson = await reader.ReadLineAsync(cancellationToken)
            ?? throw new IOException("The admin server closed the connection without a response.");

        return JsonSerializer.Deserialize<AdminResponse>(responseJson, AdminJson.Options)
            ?? throw new JsonException("The admin server returned an empty response.");
    }
}
