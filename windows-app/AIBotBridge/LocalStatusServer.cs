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
        TcpClient client, Func<StatusSnapshot> snapshot, CancellationToken cancellationToken)
    {
        try { await HandleCoreAsync(client,snapshot,cancellationToken); }
        catch(Exception ex) when(ex is IOException or SocketException or OperationCanceledException) { client.Dispose(); }
    }

    private static async Task HandleCoreAsync(
        TcpClient client,
        Func<StatusSnapshot> snapshot,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10)); cancellationToken = timeout.Token;
        using (client)
        using (var stream = client.GetStream())
        using (var reader = new StreamReader(stream, Encoding.UTF8, leaveOpen: true))
        {
            var requestLine = await reader.ReadLineAsync(cancellationToken) ?? string.Empty;
            string? line; int length = 0; bool browser = false; int headerSize = requestLine.Length;
            do
            {
                line = await reader.ReadLineAsync(cancellationToken);
                headerSize += line?.Length ?? 0;
                if (headerSize > 8192) return;
                if (line?.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)==true) int.TryParse(line.Split(':',2)[1].Trim(),out length);
                if (line?.StartsWith("Origin:",StringComparison.OrdinalIgnoreCase)==true) browser=true;
            } while (!string.IsNullOrEmpty(line));

            bool accepted=false;
            if (!browser && requestLine.StartsWith("POST /event ",StringComparison.Ordinal) && length is >0 and <=16384)
            {
                var text=new StringBuilder(); var character=new char[1];
                while(Encoding.UTF8.GetByteCount(text.ToString())<length && await reader.ReadAsync(character,cancellationToken)>0) text.Append(character[0]);
                try
                {
                    using var doc=JsonDocument.Parse(text.ToString()); var root=doc.RootElement;
                    if(Encoding.UTF8.GetByteCount(text.ToString())==length && root.ValueKind==JsonValueKind.Object &&
                       root.TryGetProperty("agent",out var agent)&&agent.ValueKind==JsonValueKind.String&&
                       root.TryGetProperty("event",out var kind)&&kind.ValueKind==JsonValueKind.String)
                        accepted=SessionActivityReader.Signals.Record(agent.GetString()??"",kind.GetString()??"",root.TryGetProperty("message",out var message)&&message.ValueKind==JsonValueKind.String?message.GetString():null);
                }
                catch(JsonException) { }
            }
            if (!browser && requestLine.StartsWith("POST /completion/ack ",StringComparison.Ordinal))
            { SessionActivityReader.Signals.Acknowledge(); accepted=true; }
            var found = requestLine.StartsWith("GET /status ", StringComparison.Ordinal);
            var pets = !browser && requestLine.StartsWith("GET /diagnostics/pets ", StringComparison.Ordinal);
            var body = pets ? JsonSerializer.Serialize(PetAnimationStore.Shared.Diagnostics(), JsonDefaults.Options) : found
                ? JsonSerializer.Serialize(snapshot(), JsonDefaults.Options)
                : accepted ? "{\"ok\":true}" : "{\"error\":\"not_found\"}";
            var payload = Encoding.UTF8.GetBytes(body);
            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {(found || pets || accepted ? "200 OK" : "404 Not Found")}\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                $"Content-Length: {payload.Length}\r\n" +
                "Connection: close\r\n\r\n");

            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(payload, cancellationToken);
        }
    }
}
