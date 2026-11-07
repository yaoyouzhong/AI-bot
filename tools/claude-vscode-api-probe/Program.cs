using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Offline protocol fixture for the VS Code wrapper calling convention. Accept
// only synthetic inputs and a loopback model. Never attach to a user's session.
Console.InputEncoding=new UTF8Encoding(false);
Console.OutputEncoding=new UTF8Encoding(false);
if (args.Length < 2 || !Path.IsPathFullyQualified(args[0]) ||
    !Uri.TryCreate(Environment.GetEnvironmentVariable("ANTHROPIC_BASE_URL"), UriKind.Absolute, out var api) ||
    api.Scheme != "http" || api.Host != "127.0.0.1" ||
    Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") != "fixture-key" ||
    !int.TryParse(Environment.GetEnvironmentVariable("AIBOT_PROBE_PORT"), out int port) || port is <1 or >65535 ||
    Environment.GetEnvironmentVariable("AIBOT_PROBE_SECRET") is not { Length:64 } secret)return 2;
bool UsesStream(string flag)=>args.Skip(1).Zip(args.Skip(2)).Any(pair=>pair.First==flag&&pair.Second=="stream-json")||args.Contains(flag+"=stream-json");
if(!UsesStream("--input-format")||!UsesStream("--output-format")){
    // The extension also invokes --version and other non-chat commands.
    var ordinary=new ProcessStartInfo(args[0]){UseShellExecute=false};
    foreach(var arg in args.Skip(1))ordinary.ArgumentList.Add(arg);
    ordinary.Environment.Remove("AIBOT_PROBE_SECRET");ordinary.Environment.Remove("AIBOT_PROBE_PORT");
    using var process=Process.Start(ordinary)??throw new IOException("fixture_cli_unavailable");
    await process.WaitForExitAsync();return process.ExitCode;
}

using var socket=new TcpClient();
using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(2));
await socket.ConnectAsync(IPAddress.Loopback,port,deadline.Token);
using var network=socket.GetStream();
using var commands=new StreamReader(network,Encoding.UTF8);
using var challengeWriter=new StreamWriter(network,new UTF8Encoding(false),leaveOpen:true){AutoFlush=true};
var challenge=RandomNumberGenerator.GetBytes(32);
await challengeWriter.WriteLineAsync(Convert.ToHexString(challenge));
var proof=await commands.ReadLineAsync(deadline.Token);
if(proof?.Length!=64 || !CryptographicOperations.FixedTimeEquals(Convert.FromHexString(proof),
    HMACSHA256.HashData(Convert.FromHexString(secret),challenge))) return 3;
await challengeWriter.WriteLineAsync("ready");

var start=new ProcessStartInfo(args[0]){UseShellExecute=false,RedirectStandardInput=true,RedirectStandardOutput=true,CreateNoWindow=true,StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=Encoding.UTF8};
foreach(string arg in args.Skip(1))start.ArgumentList.Add(arg);
foreach(string key in new[]{"AIBOT_PROBE_PORT","AIBOT_PROBE_SECRET","AIBOT_PROBE_SESSION"})start.Environment.Remove(key);
using var child=Process.Start(start)??throw new IOException("fixture_child_unavailable");
using var writeGate=new SemaphoreSlim(1);
var identity=new object();
Guid? session=null,deviceInterrupt=null;
async Task ChildOutput(){
    while(await child.StandardOutput.ReadLineAsync(deadline.Token) is {} line){
        using var json=JsonDocument.Parse(line);var frame=json.RootElement;
        string? type=frame.GetProperty("type").GetString();
        if(type=="system"&&frame.TryGetProperty("subtype",out var sub)&&sub.GetString()=="init"){
            var actual=frame.GetProperty("session_id").GetGuid();
            bool first;
            lock(identity){
                first=session is null;
                if(!first&&actual!=session)throw new IOException("fixture_session_changed");
                session=actual;
            }
            if(first)await challengeWriter.WriteLineAsync(JsonSerializer.Serialize(new{boundSession=actual}));
        }
        if(type=="result"&&frame.TryGetProperty("user_message_uuid",out var messageId)&&messageId.ValueKind==JsonValueKind.String){
            // Only fixed outcomes and IDs, never assistant/tool content.
            var message=messageId.GetString();
            var reason=frame.TryGetProperty("terminal_reason",out var reasonNode)?reasonNode.GetString():null;
            await challengeWriter.WriteLineAsync(JsonSerializer.Serialize(new{ended=message,session=frame.GetProperty("session_id").GetString(),reason}));
        }
        if(type=="control_response"&&frame.TryGetProperty("response",out var response)&&
            response.TryGetProperty("request_id",out var id)&&Guid.TryParse(id.GetString(),out var rid)){
            bool device;lock(identity)device=rid==deviceInterrupt;
            if(device){await challengeWriter.WriteLineAsync(line);continue;}
        }
        await Console.Out.WriteLineAsync(line);await Console.Out.FlushAsync();
    }
}
async Task Forward(string line) {
    await writeGate.WaitAsync(deadline.Token);
    try{await child.StandardInput.WriteLineAsync(line);await child.StandardInput.FlushAsync();}
    finally{writeGate.Release();}
}
async Task IdeInput() {
    while(await Console.In.ReadLineAsync(deadline.Token) is {} line)await Forward(line);
    await writeGate.WaitAsync(deadline.Token);
    try{child.StandardInput.Close();}finally{writeGate.Release();}
}
async Task DeviceInput() {
    bool appended=false, interrupted=false;
    while(await commands.ReadLineAsync(deadline.Token) is {} line){
        if(line.Length>4096)throw new InvalidDataException("fixture_frame_too_large");
        using var json=JsonDocument.Parse(line);
        var frame=json.RootElement;
        switch(frame.GetProperty("type").GetString()){
            case "user" when !appended && !interrupted:
                var message=frame.GetProperty("message");var content=message.GetProperty("content");
                Guid? bound;lock(identity)bound=session;
                if(!Guid.TryParse(frame.GetProperty("uuid").GetString(),out _)||
                    bound is null||frame.GetProperty("session_id").GetGuid()!=bound||message.GetProperty("role").GetString()!="user"||
                    content.GetArrayLength()!=1||content[0].GetProperty("type").GetString()!="text"||
                    content[0].GetProperty("text").GetString()!="fixture-device-append")throw new InvalidDataException("unexpected_fixture_message");
                appended=true;break;
            case "control_request" when appended && !interrupted:
                if(!Guid.TryParse(frame.GetProperty("request_id").GetString(),out var privateId)||
                    frame.GetProperty("request").GetProperty("subtype").GetString()!="interrupt")throw new InvalidDataException("unexpected_fixture_control");
                lock(identity)deviceInterrupt=privateId;
                interrupted=true;break;
            default: throw new InvalidDataException("unexpected_fixture_operation");
        }
        await Forward(line);
    }
}
try{
    var ide=Task.Run(IdeInput,deadline.Token);var device=DeviceInput();var output=ChildOutput();var ended=child.WaitForExitAsync(deadline.Token);
    var completed=await Task.WhenAny(ended,ide,device,output);
    await completed;
    if(completed!=ended)await ended;
    await output;
    return child.ExitCode;
}finally{
    deadline.Cancel();
    if(!child.HasExited)child.Kill(entireProcessTree:true);
}
