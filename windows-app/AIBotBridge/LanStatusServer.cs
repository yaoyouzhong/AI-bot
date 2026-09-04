using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class LanStatusServer
{
    private const int MaxHeaderBytes = 8192;
    private readonly TcpListener _listener;
    private readonly string _token;

    internal LanStatusServer(LanPairing pairing)
    {
        _listener = new TcpListener(pairing.Address, pairing.Port);
        _token = pairing.Token;
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

    private async Task HandleAsync(
        TcpClient client,
        Func<StatusSnapshot> snapshot,
        CancellationToken cancellationToken)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            var lines = await ReadHeaderAsync(stream, cancellationToken);
            var requestLine = lines.FirstOrDefault() ?? string.Empty;
            var suppliedToken = lines
                .FirstOrDefault(line => line.StartsWith("X-AIBot-Token:", StringComparison.OrdinalIgnoreCase))?
                .Split(':', 2)[1].Trim() ?? string.Empty;
            var authenticated = FixedTimeEquals(_token, suppliedToken);
            var found = requestLine.StartsWith("GET /status ", StringComparison.Ordinal);

            var statusCode = !authenticated ? "401 Unauthorized" : found ? "200 OK" : "404 Not Found";
            var body = authenticated && found
                ? JsonSerializer.Serialize(snapshot(), JsonDefaults.Options)
                : authenticated ? "{\"error\":\"not_found\"}" : "{\"error\":\"unauthorized\"}";
            await WriteResponseAsync(stream, statusCode, body, cancellationToken);
        }
    }

    private static async Task<IReadOnlyList<string>> ReadHeaderAsync(
        NetworkStream stream, CancellationToken cancellationToken)
    {
        var bytes = new List<byte>(512);
        var one = new byte[1];
        while (bytes.Count < MaxHeaderBytes)
        {
            if (await stream.ReadAsync(one, cancellationToken) == 0)
                break;
            bytes.Add(one[0]);
            var count = bytes.Count;
            if (count >= 4 && bytes[count - 4] == '\r' && bytes[count - 3] == '\n' &&
                bytes[count - 2] == '\r' && bytes[count - 1] == '\n')
                return Encoding.ASCII.GetString(bytes.ToArray()).Split("\r\n");
        }
        return Array.Empty<string>();
    }

    private static bool FixedTimeEquals(string expected, string actual)
    {
        var left = Encoding.UTF8.GetBytes(expected);
        var right = Encoding.UTF8.GetBytes(actual);
        return left.Length == right.Length && CryptographicOperations.FixedTimeEquals(left, right);
    }

    private static async Task WriteResponseAsync(
        NetworkStream stream, string status, string body, CancellationToken cancellationToken)
    {
        var payload = Encoding.UTF8.GetBytes(body);
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status}\r\nContent-Type: application/json; charset=utf-8\r\n" +
            $"Content-Length: {payload.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(headers, cancellationToken);
        await stream.WriteAsync(payload, cancellationToken);
    }
}
