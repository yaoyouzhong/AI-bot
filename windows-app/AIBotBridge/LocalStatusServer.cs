using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class LocalStatusServer
{
    private readonly TcpListener _listener;
    private readonly Func<UsbDeviceInfo>? _deviceInfo;
    private readonly Func<string>? _tab5Info;
    private readonly Func<string,CancellationToken,Task>? _musicArtwork;
    private readonly Func<string>? _tab5Crashes;
    private readonly Func<string>? _musicInfo;

    internal LocalStatusServer(int port, Func<UsbDeviceInfo>? deviceInfo = null, Func<string>? tab5Info = null,Func<string,CancellationToken,Task>? musicArtwork=null,Func<string>? tab5Crashes=null,Func<string>? musicInfo=null)
    {
        _listener = new TcpListener(IPAddress.Loopback, port);
        _deviceInfo = deviceInfo;
        _tab5Info = tab5Info;
        _musicArtwork=musicArtwork;
        _tab5Crashes=tab5Crashes;
        _musicInfo=musicInfo;
    }

    internal async Task RunAsync(Func<StatusSnapshot> snapshot, CancellationToken cancellationToken)
    {
        await ListenerStartup.StartAsync(_listener, cancellationToken);
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
        TcpClient client, Func<StatusSnapshot> snapshot, CancellationToken cancellationToken)
    {
        try { await HandleCoreAsync(client,snapshot,cancellationToken); }
        catch(Exception ex) when(ex is IOException or SocketException or OperationCanceledException) { client.Dispose(); }
    }

    private async Task HandleCoreAsync(
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
            string? line; int length = 0; bool browser = false; int headerSize = requestLine.Length;string? origin=null,musicKey=null;
            do
            {
                line = await reader.ReadLineAsync(cancellationToken);
                headerSize += line?.Length ?? 0;
                if (headerSize > 8192) return;
                if (line?.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase)==true) int.TryParse(line.Split(':',2)[1].Trim(),out length);
                if (line?.StartsWith("Origin:",StringComparison.OrdinalIgnoreCase)==true){browser=true;origin=line.Split(':',2)[1].Trim();}
                if(line?.StartsWith("X-AIBot-Music-Key:",StringComparison.OrdinalIgnoreCase)==true)musicKey=line.Split(':',2)[1].Trim();
            } while (!string.IsNullOrEmpty(line));

            bool accepted=false;
            if(_musicArtwork is not null&&requestLine.StartsWith("POST /music/artwork ",StringComparison.Ordinal)&&length is >0 and <=16384&&BrowserMusicArtwork.Authorized(origin,musicKey)) {
                var text=new StringBuilder();var character=new char[1];
                while(Encoding.UTF8.GetByteCount(text.ToString())<length&&await reader.ReadAsync(character,cancellationToken)>0)text.Append(character[0]);
                try {
                    if(Encoding.UTF8.GetByteCount(text.ToString())==length){await _musicArtwork(text.ToString(),cancellationToken);accepted=true;}
                }catch(Exception ex) when(ex is JsonException or ArgumentException or System.Net.Http.HttpRequestException or InvalidDataException){ }
            }
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
            // Loopback-only diagnostics; do not add device access to the LAN server.
            var device = !browser && _deviceInfo is not null && requestLine.StartsWith("GET /diagnostics/device ", StringComparison.Ordinal);
            var activity = !browser && requestLine.StartsWith("GET /diagnostics/activity ", StringComparison.Ordinal);
            string? diagnostics = null;
            if (device)
            {
                try { diagnostics = JsonSerializer.Serialize(await Task.Run(_deviceInfo!, cancellationToken), JsonDefaults.Options); }
                catch (Exception ex) when (ex is IOException or TimeoutException or InvalidOperationException)
                { diagnostics = JsonSerializer.Serialize(new { error = ex.GetType().Name }); }
            }
            if (activity) diagnostics = JsonSerializer.Serialize(SessionActivityReader.Diagnostics(), JsonDefaults.Options);
            var tab5 = !browser && _tab5Info is not null && requestLine.StartsWith("GET /diagnostics/tab5 ", StringComparison.Ordinal);
            if (tab5) diagnostics = JsonSerializer.Serialize(new { summary = _tab5Info!() });
            var crashes=!browser&&_tab5Crashes is not null&&requestLine.StartsWith("GET /diagnostics/tab5-crashes ",StringComparison.Ordinal);
            if(crashes)diagnostics=_tab5Crashes!();
            var webQuota = !browser && requestLine.StartsWith("GET /diagnostics/web-quota ", StringComparison.Ordinal);
            if (webQuota) diagnostics = JsonSerializer.Serialize(WebQuotaDiagnostics.Snapshot(), JsonDefaults.Options);
            var quotaHistory = !browser && requestLine.StartsWith("GET /diagnostics/quota-history ", StringComparison.Ordinal);
            if (quotaHistory) diagnostics = JsonSerializer.Serialize(Tab5QuotaTrend.Diagnostics(), JsonDefaults.Options);
            var musicInfo=!browser&&_musicInfo is not null&&requestLine.StartsWith("GET /diagnostics/music ",StringComparison.Ordinal);
            if(musicInfo)diagnostics=JsonSerializer.Serialize(new {summary=_musicInfo!()});
            var found = requestLine.StartsWith("GET /status ", StringComparison.Ordinal);
            var pets = !browser && requestLine.StartsWith("GET /diagnostics/pets ", StringComparison.Ordinal);
            var body = diagnostics ?? (pets ? JsonSerializer.Serialize(PetAnimationStore.Shared.Diagnostics(), JsonDefaults.Options) : found
                ? JsonSerializer.Serialize(snapshot(), JsonDefaults.Options)
                : accepted ? "{\"ok\":true}" : "{\"error\":\"not_found\"}");
            var payload = Encoding.UTF8.GetBytes(body);
            var header = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 {(found || pets || accepted || device || activity || webQuota || tab5 || crashes || quotaHistory || musicInfo ? "200 OK" : "404 Not Found")}\r\n" +
                "Content-Type: application/json; charset=utf-8\r\n" +
                $"Content-Length: {payload.Length}\r\n" +
                "Connection: close\r\n\r\n");

            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(payload, cancellationToken);
        }
    }
}
