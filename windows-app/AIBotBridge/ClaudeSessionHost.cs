using System.Buffers.Binary;
using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace AIBotBridge;

// Invoked by VS Code as its configured process wrapper. The original CLI keeps
// the IDE's cwd, arguments, environment, stdout and permission dialogue.
internal sealed class ClaudeSessionHost
{
    internal sealed record Command(Guid Session,Guid Id,string Op,Guid? Parent,string Text,string[] Images);
    internal sealed record Reply(string State,Guid? User=null);
    private sealed class Pending(Command command,string digest){
        internal readonly string Op=command.Op,Digest=digest;
        internal readonly Guid? Parent=command.Parent;
        internal readonly TaskCompletionSource<Reply> Done=new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal bool Ack,Ended;
    }
    private readonly Process child;
    private readonly object gate=new();
    private readonly SemaphoreSlim input=new(1,1);
    private readonly Dictionary<Guid,Pending> pending=[];
    private readonly List<Guid> turns=[];
    private Guid? bound;
    private bool switched;
    private bool sawUser;
    private readonly bool resumed;
    private ClaudeSessionHost(Process process,string[] args){child=process;resumed=args.Any(a=>a is "-r" or "-c" or "--continue" or "--resume" || a.StartsWith("--resume=",StringComparison.Ordinal));}
    private const int MaxFrame=10*1024*1024;
    private static readonly UTF8Encoding Utf8=new(false,true);
    internal static string HostPath=>Path.ChangeExtension(typeof(ClaudeSessionHost).Assembly.Location,".exe");
    internal static bool SameHost(Process process)=>string.Equals(process.MainModule?.FileName,HostPath,StringComparison.OrdinalIgnoreCase);
    internal static string PipeName(Process process)=>$"AI-bot.Claude.v1.{process.Id}.{process.StartTime.ToUniversalTime().ToFileTimeUtc()}";
    internal static bool Vendor(string path){
        if(!Path.IsPathFullyQualified(path)||!File.Exists(path)||!Path.GetFileName(path).Equals("claude.exe",StringComparison.OrdinalIgnoreCase))return false;
        var folder=new DirectoryInfo(Path.GetDirectoryName(path)!);
        while(folder is not null){
            if((folder.Attributes&FileAttributes.ReparsePoint)!=0)return false;
            if(folder.Name.StartsWith("anthropic.claude-code-",StringComparison.OrdinalIgnoreCase)){
                string relative=Path.GetRelativePath(folder.FullName,path).Replace('\\','/');
                return relative.StartsWith("resources/native-binar",StringComparison.Ordinal)&&File.Exists(Path.Combine(folder.FullName,"package.json"));
            }
            folder=folder.Parent;
        }
        return false;
    }
    internal static async Task<int> Run(string[] args){
        var start=new ProcessStartInfo(args[0]){UseShellExecute=false,CreateNoWindow=true};
        foreach(string arg in args.Skip(1))start.ArgumentList.Add(arg);
        bool Stream(string key)=>args.Skip(1).Zip(args.Skip(2)).Any(p=>p.First==key&&p.Second=="stream-json")||args.Contains(key+"=stream-json");
        if(!Stream("--input-format")||!Stream("--output-format")||!args.Contains("--replay-user-messages")){
            using var plain=Process.Start(start)??throw new IOException("cli_unavailable");
            await plain.WaitForExitAsync();return plain.ExitCode;
        }
        start.RedirectStandardInput=start.RedirectStandardOutput=true;
        start.StandardInputEncoding=Utf8;start.StandardOutputEncoding=Utf8;
        using var cli=Process.Start(start)??throw new IOException("cli_unavailable");
        using var stop=new CancellationTokenSource();
        var host=new ClaudeSessionHost(cli,args);
        var api=host.Serve(stop.Token);
        var ide=Task.Run(()=>host.IdeInput(stop.Token));
        var output=host.Output(stop.Token);
        var ended=cli.WaitForExitAsync();
        try{
            var first=await Task.WhenAny(ide,output,ended);await first;
            await ended;await output;return cli.ExitCode;
        }finally{
            stop.Cancel();if(!cli.HasExited)cli.Kill(entireProcessTree:true);
            try{await api;}catch(OperationCanceledException){}
        }
    }
    private static Guid? Id(JsonElement e,string key)=>Guid.TryParse(ClaudeHistory.Text(e,key),out var id)&&id!=Guid.Empty?id:null;
    private void Bind(Guid id){
        if(bound is {} before&&before!=id)switched=true;
        bound=id;
    }
    private void Enqueue(Guid id){if(!turns.Contains(id))turns.Add(id);}
    private async Task IdeInput(CancellationToken ct){
        using var source=new StreamReader(Console.OpenStandardInput(),Utf8);
        while(await source.ReadLineAsync(ct) is {} line){
            await input.WaitAsync(ct);
            try{
                using var frame=JsonDocument.Parse(line);var row=frame.RootElement;
                if(ClaudeHistory.Text(row,"type")=="user"){
                    lock(gate){sawUser=true;Enqueue(Id(row,"uuid")??Guid.Empty);}
                }
                await child.StandardInput.WriteLineAsync(line);await child.StandardInput.FlushAsync();
            }finally{input.Release();}
        }
        await input.WaitAsync(ct);try{child.StandardInput.Close();}finally{input.Release();}
    }
    private async Task Output(CancellationToken ct){
        using var ide=new StreamWriter(Console.OpenStandardOutput(),Utf8){AutoFlush=true};
        while(await child.StandardOutput.ReadLineAsync(ct) is {} line){
            bool show=true;
            try{using var frame=JsonDocument.Parse(line);lock(gate)show=Observe(frame.RootElement);}
            catch(JsonException){/* Forward unrecognized vendor output unchanged. */}
            if(show)await ide.WriteLineAsync(line);
        }
    }
    private bool Observe(JsonElement row){
        string? type=ClaudeHistory.Text(row,"type");
        if(type=="system"&&ClaudeHistory.Text(row,"subtype")=="init"&&Id(row,"session_id") is {} sid)Bind(sid);
        if(type=="control_response"&&row.TryGetProperty("response",out var response)&&Id(response,"request_id") is {} rid&&pending.TryGetValue(rid,out var ctl)&&ctl.Op=="interrupt"){
            ctl.Ack=ClaudeHistory.Text(response,"subtype")=="success";CompleteStop(ctl);return false;
        }
        if(switched||Id(row,"session_id")!=bound||bound is null)return true;
        // A queued prompt is not written into the transcript until the prior
        // turn finishes. The native queue nevertheless acknowledges its exact
        // command UUID and session immediately. Never treat writing stdin as
        // this acknowledgement or use it as evidence that a turn has started.
        if(type=="command_lifecycle"&&ClaudeHistory.Text(row,"state")=="queued"&&Id(row,"command_uuid") is {} queued&&
            pending.TryGetValue(queued,out var queuedItem)&&queuedItem.Op=="steer")
            queuedItem.Done.TrySetResult(new("accepted",queued));
        if(type=="user"&&Id(row,"uuid") is {} user){
            sawUser=true;
            if(turns.Count>0&&turns[0]==Guid.Empty)turns[0]=user;
            if(pending.TryGetValue(user,out var item)&&row.TryGetProperty("message",out var message)&&message.TryGetProperty("content",out var blocks)){
                if(ClaudeHistory.Text(message,"role")=="user"&&ClaudeNativeContent.MessageDigest(blocks)==item.Digest)
                    item.Done.TrySetResult(new("accepted",user));
            }
        }
        if(type=="result"){
            var finished=new HashSet<Guid>();if(Id(row,"user_message_uuid") is {} one)finished.Add(one);
            if(row.TryGetProperty("user_message_uuids",out var ids)&&ids.ValueKind==JsonValueKind.Array)
                foreach(var v in ids.EnumerateArray())if(v.ValueKind==JsonValueKind.String&&Guid.TryParse(v.GetString(),out var id))finished.Add(id);
            turns.RemoveAll(finished.Contains);
            if(ClaudeHistory.Text(row,"terminal_reason") is "interrupted" or "aborted_streaming"){
                foreach(var stop in pending.Values.Where(p=>p.Op=="interrupt"&&p.Parent.HasValue&&finished.Contains(p.Parent.Value))){stop.Ended=true;CompleteStop(stop);}
            }
        }
        return true;
    }
    private static void CompleteStop(Pending p){if(p.Ack&&p.Ended)p.Done.TrySetResult(new("stopped"));}
    internal static async Task<JsonElement> ReadFrame(Stream stream,int max,CancellationToken ct){
        var header=new byte[4];await stream.ReadExactlyAsync(header,ct);
        int size=BinaryPrimitives.ReadInt32LittleEndian(header);
        if(size<2||size>max)throw new IOException("invalid_frame");
        var bytes=new byte[size];await stream.ReadExactlyAsync(bytes,ct);
        using var data=JsonDocument.Parse(bytes);return data.RootElement.Clone();
    }
    internal static async Task WriteFrame(Stream stream,object value,CancellationToken ct){
        byte[] bytes=JsonSerializer.SerializeToUtf8Bytes(value);
        if(bytes.Length>MaxFrame)throw new IOException("frame_too_large");
        byte[] header=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(header,bytes.Length);
        await stream.WriteAsync(header,ct);await stream.WriteAsync(bytes,ct);await stream.FlushAsync(ct);
    }
    private async Task Serve(CancellationToken ct){
        await Task.WhenAll(Enumerable.Range(0,4).Select(_=>Listen(ct)));
    }
    private async Task Listen(CancellationToken ct){
        while(!ct.IsCancellationRequested){
            using var pipe=new NamedPipeServerStream(PipeName(child),PipeDirection.InOut,4,PipeTransmissionMode.Byte,PipeOptions.CurrentUserOnly|PipeOptions.Asynchronous);
            try{
                await pipe.WaitForConnectionAsync(ct);
                if(!GetNamedPipeClientProcessId(pipe.SafePipeHandle,out int pid))continue;
                using var peer=Process.GetProcessById(pid);
                if(!SameHost(peer))continue;
                using var limit=CancellationTokenSource.CreateLinkedTokenSource(ct);limit.CancelAfter(TimeSpan.FromSeconds(55));
                var json=await ReadFrame(pipe,MaxFrame,limit.Token);
                var cmd=json.Deserialize<Command>()??throw new IOException("invalid_command");
                var reply=await Submit(cmd,limit.Token);await WriteFrame(pipe,reply,limit.Token);
            }catch(Exception e) when(e is IOException or JsonException or ArgumentException or InvalidOperationException or UnauthorizedAccessException or OperationCanceledException or System.ComponentModel.Win32Exception or FormatException){/* One connection; never repeat a mutation. */}
        }
    }
    private async Task<Reply> Submit(Command cmd,CancellationToken ct){
        if(cmd.Op=="active"){lock(gate)return !switched&&bound==cmd.Session&&turns.Count==1&&turns[0]==cmd.Parent&&
            ClaudeSessionApi.Metadata(child,cmd.Session) is "busy" or "working"?new("active"):new("unknown");}
        if(cmd.Op=="fresh"){lock(gate)return !switched&&!resumed&&!sawUser&&turns.Count==0&&(!bound.HasValue||bound==cmd.Session)&&ClaudeSessionApi.Metadata(child,cmd.Session)=="idle"?new("fresh"):new("unknown");}
        if(cmd.Op=="receipt"){lock(gate)return !switched&&bound==cmd.Session&&pending.TryGetValue(cmd.Id,out var known)&&known.Done.Task.IsCompletedSuccessfully?known.Done.Task.Result:new("unknown");}
        if(cmd.Id==Guid.Empty||cmd.Session==Guid.Empty||cmd.Op is not ("send" or "steer" or "interrupt")||
            cmd.Text is null||cmd.Images is null||cmd.Images.Length>3||Encoding.UTF8.GetByteCount(cmd.Text)>2000)return new("rejected");
        if(cmd.Op=="interrupt"?(cmd.Text.Length!=0||cmd.Images.Length!=0):(cmd.Text.Length==0&&cmd.Images.Length==0))return new("rejected");
        if(cmd.Op!="send"&&cmd.Parent is null)return new("rejected");
        var blocks=new List<object>();var digests=new List<string>();
        if(cmd.Text.Length>0)blocks.Add(new{type="text",text=cmd.Text});
        foreach(var image in cmd.Images){
            if(image is null||image.Length>4*((Tab5CodexImages.MaxBytes+2)/3))return new("rejected");
            var bytes=Convert.FromBase64String(image);digests.Add(ClaudeNativeContent.ImageDigest(bytes));
            string mime=bytes[0]==0xff?"image/jpeg":"image/png";
            blocks.Add(new{type="image",source=new{type="base64",media_type=mime,data=image}});
        }
        object frame=cmd.Op=="interrupt"?new{type="control_request",request_id=cmd.Id,request=new{subtype="interrupt",cancel_queued=true}}:
            new{type="user",session_id=cmd.Session,uuid=cmd.Id,parent_tool_use_id=(string?)null,message=new{role="user",content=blocks}};
        await input.WaitAsync(ct);Pending item;
        try{
            // Scope/turn and input ordering are checked inside the same write gate as IDE messages.
            var state=ClaudeSessionApi.Metadata(child,cmd.Session);
            lock(gate){
                if(switched||state is null||bound.HasValue&&bound!=cmd.Session||pending.Count>=512||pending.ContainsKey(cmd.Id))return new("rejected");
                if(cmd.Op=="send"?(turns.Count!=0||state!="idle"):
                    (state is not ("working" or "busy")||turns.Count!=1||turns[0]!=cmd.Parent))return new("rejected");
                Bind(cmd.Session);item=new(cmd,ClaudeNativeContent.Digest(cmd.Text,digests));pending.Add(cmd.Id,item);
                if(cmd.Op!="interrupt"){sawUser=true;Enqueue(cmd.Id);}
            }
            await child.StandardInput.WriteLineAsync(JsonSerializer.Serialize(frame));await child.StandardInput.FlushAsync();
        }finally{input.Release();}
        try{return await item.Done.Task.WaitAsync(TimeSpan.FromSeconds(12),ct);}catch(TimeoutException){return new("unknown");}
    }
    [DllImport("kernel32.dll",SetLastError=true)]internal static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe,out int pid);
    [DllImport("kernel32.dll",SetLastError=true)]private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe,out int pid);
}
