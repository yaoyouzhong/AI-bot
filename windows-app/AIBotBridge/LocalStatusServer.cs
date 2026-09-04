using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class LocalStatusServer
{
    private readonly TcpListener _listener;

    internal LocalStatusServer(int port)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
    }

    internal async Task RunAsync(Func<StatusSnapshot> snapshot, CancellationToken cancellationToken)
    {
        _listener.Start();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                _ = HandleAsync(client, snapshot, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            _listener.Stop();
        }
    }

    private static async Task HandleAsync(
        TcpClient client,
        Func<StatusSnapshot> snapshot,
        CancellationToken cancellationToken)
    {
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true))
        {
            var requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
            string? line;
            do
            {
                line = await reader.ReadLineAsync(cancellationToken);
            } while (!string.IsNullOrEmpty(line));

            var found = requestLine.StartsWith("GET /status ", StringComparison.Ordinal);
            var body = found
                ? JsonSerializer.Serialize(snapshot(), JsonDefaults.Options)
                : "{\"error\":\"not_found\"}";
            var payload = Encoding.UTF8.GetBytes(body);
            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {(found ? "200 OK" : "404 Not Found")}\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                $"Content-Length: {payload.Length}\r\n" +
                "Connection: close\r\n\r\n");

            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(payload, cancellationToken);
        }
    }
}
