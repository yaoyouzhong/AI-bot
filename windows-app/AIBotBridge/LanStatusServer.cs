using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class LanStatusServer
{

    private readonly TcpListener _listener;
    private readonly string _token;
    private readonly Func<IReadOnlyList<ResourcePayload>> _resources;
    private readonly Func<Tab5Service?> _tab5Provider;
    private Tab5Service? _tab5 => _tab5Provider();
    private readonly Func<bool> _legacyEnabled;
    private readonly Action? _legacyActivity;
    private readonly Action<string?>? _legacyFirmware;

    internal LanStatusServer(LanPairing pairing, Func<IReadOnlyList<ResourcePayload>>? resources = null, Tab5Service? tab5 = null, Func<Tab5Service?>? tab5Provider = null, Func<bool>? legacyEnabled = null, Action? legacyActivity = null, Action<string?>? legacyFirmware = null)
    {
        _listener = new TcpListener(pairing.Address, pairing.Port);
        _token = pairing.Token;
        _resources = resources ?? (() => []);
        _tab5Provider=tab5Provider??(()=>tab5);_legacyEnabled=legacyEnabled??(()=>true);_legacyActivity=legacyActivity;
        _legacyFirmware=legacyFirmware;
    }

    internal async Task RunAsync(Func<StatusSnapshot> snapshot, CancellationToken cancellationToken,
        Action? onListening = null)
    {
        await ListenerStartup.StartAsync(_listener, cancellationToken);
        onListening?.Invoke();
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);
                client.NoDelay = true;
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
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(60));
        try { await HandleCoreAsync(client,snapshot,timeout,timeout.Token); }
        catch(Exception ex) when(ex is IOException or SocketException or OperationCanceledException) { client.Dispose(); }
    }

    private async Task HandleCoreAsync(
        TcpClient client,
        Func<StatusSnapshot> snapshot,
        CancellationTokenSource timeout,
        CancellationToken cancellationToken)
    {
        using (client)
        using (var stream = client.GetStream())
        {
            var input = new HttpConnectionReader(stream);
            var lines = await input.ReadHeaderAsync(cancellationToken);
            var requestLine = lines.FirstOrDefault() ?? string.Empty;
            var tab5=_tab5;
            using var deviceStop=requestLine.Contains(" /tab5/",StringComparison.Ordinal)&&tab5 is not null?CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,tab5.LifetimeToken):null;
            if(deviceStop is not null)cancellationToken=deviceStop.Token;
            if(requestLine=="POST /tab5/v1/rpc HTTP/1.1") {
                await HandleVoiceAsync(stream,input,lines,timeout,cancellationToken,rpc:true);
                return;
            }
            if(requestLine=="POST /tab5/v1/voice HTTP/1.1") {
                await HandleVoiceAsync(stream,input,lines,timeout,cancellationToken);
                return;
            }
            if (requestLine == "GET /tab5/v1/status HTTP/1.1")
            {
                string Header(string name) => lines.FirstOrDefault(l => l.StartsWith(name+":",StringComparison.OrdinalIgnoreCase))?.Split(':',2)[1].Trim()??"";
                var packet = tab5?.Respond(Header("X-AIBot-Device"),Header("X-AIBot-Nonce"),Header("X-AIBot-Proof"),Header("X-AIBot-Assets"),Header("X-AIBot-Assets-Proof"),Header("X-Tab5-Firmware"),Header("X-Tab5-Firmware-Proof"));
                var diagPath=Environment.GetEnvironmentVariable("AIBOT_TAB5_DIAG_LOG");
                var diag=Header("X-Tab5-Diag");
                if(packet is not null&&diagPath is not null&&diag.Length is >0 and <160&&diag.All(c=>char.IsAsciiDigit(c)||c is ',' or '-'))
                {
                    try { File.AppendAllText(diagPath,$"{DateTimeOffset.Now:O} DEV {diag}{Environment.NewLine}"); }
                    catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) { }
                }
                if (packet is null) await WriteResponseAsync(stream,"401 Unauthorized","{\"error\":\"unavailable_or_unauthorized\"}",cancellationToken);
                else await WriteBytesAsync(stream,"200 OK",packet,cancellationToken);
                return;
            }
            if(requestLine.StartsWith("GET /tab5/v1/ota/",StringComparison.Ordinal)&&requestLine.EndsWith(" HTTP/1.1",StringComparison.Ordinal)) {
                string Header(string name)=>lines.FirstOrDefault(l=>l.StartsWith(name+":",StringComparison.OrdinalIgnoreCase))?.Split(':',2)[1].Trim()??"";
                var sha=requestLine["GET /tab5/v1/ota/".Length..^" HTTP/1.1".Length];
                var image=tab5?.OtaImage(Header("X-AIBot-Device"),Header("X-AIBot-Nonce"),Header("X-AIBot-Proof"),sha);
                if(image is null)await WriteResponseAsync(stream,"401 Unauthorized","{\"error\":\"ota_unavailable_or_unauthorized\"}",cancellationToken);
                else {
                    timeout.CancelAfter(TimeSpan.FromMinutes(6));
                    await tab5!.TransferOtaAsync(stream,image,Header("X-AIBot-Device"),Header("X-AIBot-OTA-Flow"),cancellationToken,Header("X-AIBot-OTA-Encoding"));
                }
                return;
            }
            if (requestLine is "POST /tab5/v1/codex/turn HTTP/1.1" or "POST /tab5/v1/codex/read HTTP/1.1" or "POST /tab5/v1/codex/image HTTP/1.1")
            {
                bool imageOnly=requestLine=="POST /tab5/v1/codex/image HTTP/1.1";
                string Header(string name) => lines.FirstOrDefault(l => l.StartsWith(name+":",StringComparison.OrdinalIgnoreCase))?.Split(':',2)[1].Trim()??"";
                if (!int.TryParse(Header("Content-Length"), out var length) || length<28 || length>(imageOnly?Tab5CodexImages.MaxPacket:8192))
                {
                    // Drain only bounded photo-sized bodies before closing, so TCP
                    // does not reset the socket and hide the explicit 413 response.
                    if(length is >8192 and <=Tab5CodexImages.MaxPacket) {
                        byte[] discard=new byte[8192];int remaining=length;
                        while(remaining>0){int count=await input.ReadAsync(discard.AsMemory(0,Math.Min(discard.Length,remaining)),cancellationToken);if(count==0)break;remaining-=count;}
                    }
                    await WriteResponseAsync(stream,"413 Content Too Large","{\"error\":\"invalid_length\"}",cancellationToken);
                    return;
                }
                var packet=new byte[length];
                await input.ReadExactlyAsync(packet,cancellationToken);
                bool replyRead=requestLine.Contains("/codex/read ",StringComparison.Ordinal);
                long readStarted=Environment.TickCount64;
                if(replyRead)tab5?.RecordHttpRead("processing",readStarted);
                try {
                var result=tab5 is null ? (Status:503,Body:(object)new {error="tab5_unavailable"}) :
                    await tab5.SubmitCodexAsync(Header("X-AIBot-Device"),Header("X-AIBot-Nonce"),Header("X-AIBot-Proof"),packet,cancellationToken,requestLine.Contains("/codex/read ",StringComparison.Ordinal),imageOnly);
                string replyBody=JsonSerializer.Serialize(result.Body,JsonDefaults.Options);
                int bytes=System.Text.Encoding.UTF8.GetByteCount(replyBody);
                if(replyRead)tab5?.RecordHttpRead("sending",readStarted,result.Status,bytes);
                await WriteResponseAsync(stream,result.Status switch {200=>"200 OK",202=>"202 Accepted",400=>"400 Bad Request",401=>"401 Unauthorized",404=>"404 Not Found",409=>"409 Conflict",429=>"429 Too Many Requests",504=>"504 Gateway Timeout",_=>"503 Service Unavailable"},
                    replyBody,cancellationToken);
                if(replyRead)tab5?.RecordHttpRead("sent",readStarted,result.Status,bytes);
                } catch(Exception ex) when(ex is IOException or SocketException or OperationCanceledException) {
                    if(replyRead)tab5?.RecordHttpRead("failed "+ex.GetType().Name,readStarted);
                    throw;
                }
                return;
            }
            if(!_legacyEnabled()){await WriteResponseAsync(stream,"404 Not Found","{\"error\":\"device_disabled\"}",cancellationToken);return;}
            var suppliedToken = lines
                .FirstOrDefault(line => line.StartsWith("X-AIBot-Token:", StringComparison.OrdinalIgnoreCase))?
                .Split(':', 2)[1].Trim() ?? string.Empty;
            var authenticated = FixedTimeEquals(_token, suppliedToken);
            var requestParts = requestLine.Split(' ');
            if (authenticated && requestParts.Length == 3 && requestParts[0] == "GET" &&
                (requestParts[1] == "/resources" || requestParts[1].StartsWith("/resources/", StringComparison.Ordinal)))
            {
                var resource = LanResourceCatalog.Respond(requestParts[1], _resources());
                await WriteBytesAsync(stream, resource.Status, resource.Body, cancellationToken);
                return;
            }
            var found = requestLine.StartsWith("GET /status ", StringComparison.Ordinal);

            var statusCode = !authenticated ? "401 Unauthorized" : found ? "200 OK" : "404 Not Found";
            var body = authenticated && found
                ? JsonSerializer.Serialize(snapshot(), JsonDefaults.Options)
                : authenticated ? "{\"error\":\"not_found\"}" : "{\"error\":\"unauthorized\"}";
            await WriteResponseAsync(stream, statusCode, body, cancellationToken);
            if(authenticated&&found) {
                _legacyFirmware?.Invoke(lines.FirstOrDefault(line=>line.StartsWith("X-AIBot-Firmware:",StringComparison.OrdinalIgnoreCase))?.Split(':',2)[1].Trim());
                _legacyActivity?.Invoke();
            }
        }
    }

    private async Task HandleVoiceAsync(NetworkStream stream, HttpConnectionReader input, IReadOnlyList<string> lines,
        CancellationTokenSource timeout, CancellationToken cancellationToken,bool rpc=false)
    {
        // One authenticated request per audio chunk, on one TCP connection for
        // the voice session. Closing every 200 ms exhausts TAB5's lwIP memory.
        timeout.CancelAfter(TimeSpan.FromSeconds(75));
        for (int requests=0; requests<400; requests++)
        {
            string Header(string name)=>lines.FirstOrDefault(l=>l.StartsWith(name+":",StringComparison.OrdinalIgnoreCase))?.Split(':',2)[1].Trim()??"";
            if(!int.TryParse(Header("Content-Length"),out int length)||length<28||length>(rpc?Tab5RpcBinary.RequestMaximum:16384)) {
                await WriteResponseAsync(stream,"413 Content Too Large","{}",cancellationToken);return;
            }
            var packet=new byte[length];await input.ReadExactlyAsync(packet,cancellationToken);
            var reply=_tab5 is null?(Status:401,Packet:(byte[]?)null):rpc?
                await _tab5.RpcAsync(Header("X-AIBot-Device"),Header("X-AIBot-Nonce"),Header("X-AIBot-Proof"),packet,cancellationToken,allowOta:false,transport:"Wi-Fi"):
                await _tab5.VoiceAsync(Header("X-AIBot-Device"),Header("X-AIBot-Nonce"),Header("X-AIBot-Proof"),packet,cancellationToken);
            if(reply.Packet is null) {await WriteResponseAsync(stream,"401 Unauthorized","{}",cancellationToken);return;}
            await WriteBytesAsync(stream,"200 OK",reply.Packet,cancellationToken,keepAlive:true);
            lines=await input.ReadHeaderAsync(cancellationToken);
            if(lines.FirstOrDefault()!=(rpc?"POST /tab5/v1/rpc HTTP/1.1":"POST /tab5/v1/voice HTTP/1.1"))return;
        }
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
        await WriteBytesAsync(stream, status, payload, cancellationToken, "application/json; charset=utf-8");
    }
    private static async Task WriteBytesAsync(NetworkStream stream, string status, byte[] payload, CancellationToken cancellationToken, string contentType="application/octet-stream", bool keepAlive=false)
    {
        var headers = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status}\r\nContent-Type: {contentType}\r\n" +
            $"Content-Length: {payload.Length}\r\nConnection: {(keepAlive?"keep-alive":"close")}\r\n\r\n");
        // A voice reply must arrive as one TCP write. Separate header and body
        // writes can wait for a delayed ACK on every 200 ms audio exchange.
        if (keepAlive) {
            var response = new byte[headers.Length + payload.Length];
            Buffer.BlockCopy(headers, 0, response, 0, headers.Length);
            Buffer.BlockCopy(payload, 0, response, headers.Length, payload.Length);
            await stream.WriteAsync(response, cancellationToken);
        } else {
            await stream.WriteAsync(headers, cancellationToken);
            await stream.WriteAsync(payload, cancellationToken);
        }
    }
}
