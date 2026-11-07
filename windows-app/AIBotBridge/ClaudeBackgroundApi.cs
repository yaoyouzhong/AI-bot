using System.Diagnostics;
using System.Globalization;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

// The native Claude supervisor's reply operation addresses an existing job.
// Never dispatch/resume a job or attach a terminal as a fallback.
internal sealed class ClaudeBackgroundApi : IClaudeNativeSession
{
    public ClaudeSessionSource Source=>ClaudeSessionSource.Background;
    private const string SupportedVersion="2.1.319";
    private static readonly string Home=AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static readonly string Root=Path.Combine(Home,".claude");
    private readonly Guid session;
    private readonly int workerPid,daemonPid;
    private readonly long workerStart,daemonStart;
    private readonly string job;
    private string? interruptAcceptanceScope;

    private ClaudeBackgroundApi(Guid session,string job,int worker,long workerStart,int daemon,long daemonStart){
        this.session=session;this.job=job;workerPid=worker;this.workerStart=workerStart;daemonPid=daemon;this.daemonStart=daemonStart;
    }

    private static bool Expected(Exception e)=>e is IOException or UnauthorizedAccessException or InvalidOperationException or
        ArgumentException or JsonException or FormatException or System.ComponentModel.Win32Exception or TimeoutException;
    private static JsonElement Record(string path){
        var file=new FileInfo(path);
        if(!file.Exists||file.Length<=0||file.Length>65536||(file.Attributes&FileAttributes.ReparsePoint)!=0||
           (file.Directory!.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("native_record_unavailable");
        using var input=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        using var json=JsonDocument.Parse(input);
        return json.RootElement.Clone();
    }
    private static string Str(JsonElement row,string name)=>ClaudeHistory.Text(row,name)??"";
    private static long Birth(JsonElement row,string key)=>long.Parse(Str(row,key),CultureInfo.InvariantCulture);
    private static string RuntimePath(int pid)=>Path.Combine(Root,"sessions",pid+".json");
    private static bool VerifiedProcess(int pid,long birth){
        using var p=Process.GetProcessById(pid);
        string? executable=p.MainModule?.FileName;
        return !p.HasExited&&p.StartTime.ToUniversalTime().ToFileTimeUtc()==birth&&p.ProcessName.Equals("claude",StringComparison.OrdinalIgnoreCase)&&
            string.Equals(executable,Path.Combine(Home,".local","bin","claude.exe"),StringComparison.OrdinalIgnoreCase);
    }
    internal static IEnumerable<Guid> LiveSessions(){
        string path=Path.Combine(Root,"sessions");
        if(!Directory.Exists(path))yield break;
        foreach(var file in Directory.EnumerateFiles(path,"*.json").Take(2048)){
            Guid? id=null;
            try{var row=Record(file);if(Str(row,"kind")=="bg"&&Guid.TryParse(Str(row,"sessionId"),out var session))id=session;}
            catch(Exception ex) when(Expected(ex)){}
            if(id.HasValue)yield return id.Value;
        }
    }
    internal static ClaudeBackgroundApi? Find(Guid session){
        try{
            string directory=Path.Combine(Root,"sessions");
            var matches=new List<JsonElement>();
            foreach(var path in Directory.EnumerateFiles(directory,"*.json").Take(2048)){
                JsonElement row;
                try{row=Record(path);}catch(Exception e) when(Expected(e)){continue;}
                if(Guid.TryParse(Str(row,"sessionId"),out var id)&&id==session&&int.TryParse(Path.GetFileNameWithoutExtension(path),out int pid)&&
                   row.GetProperty("pid").GetInt32()==pid&&VerifiedProcess(pid,Birth(row,"procStart")))matches.Add(row);
            }
            if(matches.Count!=1)return null;
            var r=matches[0];string job=Str(r,"jobId");
            if(job.Length!=8||!job.All(Uri.IsHexDigit))return null;
            var d=Record(Path.Combine(Root,"daemon.lock"));
            var client=new ClaudeBackgroundApi(session,job,r.GetProperty("pid").GetInt32(),Birth(r,"procStart"),d.GetProperty("pid").GetInt32(),Birth(d,"procStartFt"));
            _=client.State;return client;
        }catch(Exception e) when(Expected(e)||e is KeyNotFoundException){return null;}
    }
    internal static ClaudeBackgroundApi? FindForInterruptAcceptance(Guid session,string expectedScope){
        var client=Find(session);
        if(client is null||client.Scope()!=expectedScope)return null;
        client.interruptAcceptanceScope=expectedScope;
        return client;
    }
    public string State {
        get {
            var r=Record(RuntimePath(workerPid));var d=Record(Path.Combine(Root,"daemon.lock"));
            var target=Record(Path.Combine(Root,"jobs",job,"state.json"));
            if(Str(r,"kind")!="bg"||Str(r,"entrypoint")!="cli"||Str(r,"jobId")!=job||
                Str(r,"version")!=SupportedVersion||Str(d,"version")!=SupportedVersion||Str(target,"backend")!="daemon"||
                !Guid.TryParse(Str(target,"sessionId"),out var active)||active!=session||
                !Guid.TryParse(Str(r,"sessionId"),out var sid)||sid!=session||r.GetProperty("pid").GetInt32()!=workerPid||
                Birth(r,"procStart")!=workerStart||d.GetProperty("pid").GetInt32()!=daemonPid||Birth(d,"procStartFt")!=daemonStart||
                !VerifiedProcess(workerPid,workerStart)||!VerifiedProcess(daemonPid,daemonStart))throw new IOException("native_background_identity_changed");
            return Str(r,"status");
        }
    }
    public string Scope(){_=State;return ClaudeNativeContent.Hash(Encoding.UTF8.GetBytes($"bg1:{session}:{job}:{workerPid}:{workerStart}:{daemonPid}:{daemonStart}"));}
    public bool HasSessionApi=>true;
    public bool CanAppend=>true;
    public bool CanImages=>true;
    public bool UsesFileImageReferences=>true;
    public bool CanInterrupt=>interruptAcceptanceScope is not null;
    internal sealed record FileImage(string Path,string Digest);
    internal static string Reference(string path){
        if(!Path.IsPathFullyQualified(path)||path.Any(c=>c=='"'||char.IsControl(c)))throw new IOException("native_image_path_invalid");
        string normal=path.Replace('\\','/');
        return normal.Any(char.IsWhiteSpace)?"@\""+normal+"\"":"@"+normal;
    }
    internal static string? ReferencePrompt(JsonElement content){
        string? value=content.ValueKind==JsonValueKind.String?content.GetString():
            content.ValueKind==JsonValueKind.Array&&content.GetArrayLength()==1&&Str(content[0],"type")=="text"?Str(content[0],"text"):null;
        return value is {Length:>0 and <=8192}&&value.StartsWith('@')?value:null;
    }
    internal static FileImage? AttachedImage(JsonElement row){
        if(!row.TryGetProperty("attachment",out var a)||Str(a,"type")!="file"||!a.TryGetProperty("content",out var c)||
           Str(c,"type")!="image"||!c.TryGetProperty("file",out var f)||Str(f,"type") is not ("image/png" or "image/jpeg"))return null;
        string path=Str(a,"filename"),encoded=Str(f,"base64");
        if(!Path.IsPathFullyQualified(path)||path.Any(ch=>ch=='"'||char.IsControl(ch))||
           !Guid.TryParseExact(Path.GetFileNameWithoutExtension(path),"D",out _)||encoded.Length>4*((Tab5CodexImages.MaxBytes+2)/3))return null;
        try{return new(path,ClaudeNativeContent.ImageDigest(Convert.FromBase64String(encoded)));}
        catch(Exception ex) when(Expected(ex)||ex is System.Runtime.InteropServices.ExternalException){return null;}
    }
    internal static string? ReferenceDigest(string? text,IReadOnlyList<FileImage> files){
        if(text==null||files.Count is 0 or >Tab5CodexImages.MaxCount)return null;
        string prefix=string.Join(" ",files.Select(image=>Reference(image.Path)));
        if(text.Equals(prefix,StringComparison.OrdinalIgnoreCase))return ClaudeNativeContent.Digest("",files.Select(f=>f.Digest).ToArray());
        if(!text.StartsWith(prefix+" ",StringComparison.OrdinalIgnoreCase))return null;
        return ClaudeNativeContent.Digest(text[(prefix.Length+1)..],files.Select(f=>f.Digest).ToArray());
    }

    private async Task<NamedPipeClientStream> Connect(CancellationToken ct){
        var names=Directory.GetFiles(@"\\.\pipe\","cc-daemon-*-control").Select(Path.GetFileName).Where(n=>n is not null).ToArray();
        foreach(var name in names.Take(8)){
            var pipe=new NamedPipeClientStream(".",name!,PipeDirection.InOut,PipeOptions.Asynchronous|PipeOptions.CurrentUserOnly,TokenImpersonationLevel.Identification);
            try{
                await pipe.ConnectAsync(900,ct);
                if(ClaudeSessionHost.GetNamedPipeServerProcessId(pipe.SafePipeHandle,out int peer)&&peer==daemonPid&&VerifiedProcess(peer,daemonStart))return pipe;
            }catch(TimeoutException){}
            catch{pipe.Dispose();throw;}
            pipe.Dispose();
        }
        throw new IOException("native_background_pipe_unavailable");
    }
    private static string Auth(){
        var file=new FileInfo(Path.Combine(Root,"daemon","control.key"));
        if(!file.Exists||file.Length is <32 or >512||(file.Attributes&FileAttributes.ReparsePoint)!=0||
            (file.Directory!.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("native_background_auth_unavailable");
        string token=File.ReadAllText(file.FullName).Trim();
        if(token.Length is <32 or >128||!token.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_'))throw new IOException("native_background_auth_unavailable");
        return token;
    }
    internal static bool IsLiteral(string text)=>!string.IsNullOrWhiteSpace(text)&&!text.TrimStart().StartsWith('/')&&!text.TrimStart().StartsWith('!')&&!text.Contains('\0');
    public Task<ClaudeNativeSend.Result> SendAsync(string text,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token)=>
        ExecuteAsync(new("send",text,[],[],()=>true),history,dispatch,authorized,token);
    public async Task<ClaudeNativeSend.Result> ExecuteAsync(ClaudeNativeRequest request,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token){
        if(request.Operation=="interrupt"){
            if(!CanInterrupt)return new(false,false,null,"native_operation_unavailable");
            return await Interrupt(request,history,dispatch,authorized,token);
        }
        if(request.Operation is not ("send" or "steer")||request.Images.Length!=request.ImageDigests.Length||request.Images.Length>Tab5CodexImages.MaxCount||
            !(request.Text.Length==0&&request.Images.Length>0)&&!IsLiteral(request.Text))return new(false,false,null,"native_input_unsupported");
        bool submitted=false;
        using var limit=CancellationTokenSource.CreateLinkedTokenSource(token);limit.CancelAfter(TimeSpan.FromSeconds(12));
        try{
            bool Ready()=>request.Operation=="send"?State=="idle":State is "working" or "busy";
            if(!authorized()||!Ready()||!request.Current())return new(false,false,null,"turn_changed");
            using var pipe=await Connect(limit.Token);
            var before=ClaudeNativeReceipt.Capture(history(),session,Scope(),request.Text,request.ImageDigests);
            if(before.Limited||!authorized()||!request.Current()||!Ready())return new(false,false,null,"turn_changed");
            string text=request.Text;
            if(request.Images.Length>0){
                if(!request.Images.Select(ClaudeNativeContent.FileDigest).SequenceEqual(request.ImageDigests))return new(false,false,null,"native_image_changed");
                text=string.Join(" ",request.Images.Select(Reference))+(text.Length==0?"":" "+text);
            }
            var frame=JsonSerializer.SerializeToUtf8Bytes(new{op="reply",proto=1,@short=job,text,nextTurn=true,auth=Auth()});
            dispatch();submitted=true;
            await pipe.WriteAsync(frame,limit.Token);await pipe.WriteAsync(new byte[]{10},limit.Token);await pipe.FlushAsync(limit.Token);
            using var response=await ReadReply(pipe,limit.Token);
            if(!response.RootElement.TryGetProperty("ok",out var ok)||ok.ValueKind!=JsonValueKind.True||Str(response.RootElement,"op")!="reply")return new(true,true,null,"delivery_unknown");
            while(!limit.IsCancellationRequested){
                if(ClaudeNativeReceipt.Observe(history(),before,Scope()).Confirm(before) is {} user)return new(true,true,user,"");
                await Task.Delay(160,limit.Token);
            }
        }catch(Exception e) when(Expected(e)||e is OperationCanceledException or KeyNotFoundException){}
        return new(submitted,submitted,null,submitted?"delivery_unknown":"native_session_api_unavailable");
    }
    private async Task<ClaudeNativeSend.Result> Interrupt(ClaudeNativeRequest request,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token){
        if(request.ParentUser is not {} user||request.Id is not {} id||request.Images.Length!=0||request.Text.Length!=0)
            return new(false,false,null,"native_input_unsupported");
        bool written=false;
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(15));
        try{
            bool Running()=>authorized()&&request.Current()&&State is "busy" or "working";
            ClaudeNativeSend.Result BeforeWrite(string phase)=>new(false,false,null,"turn_changed"){FailureStage=phase};
            if(!Running())return BeforeWrite("original_turn_inactive");
            var before=history();
            var latest=before.Turns.LastOrDefault();
            if(before.Limited||!before.Accepted.Contains(user.ToString())||latest==null||latest["status"] is "completed" or "failed" or "interrupted")return BeforeWrite("history_not_running");
            string target=(string)latest["id"];string scope=Scope();
            using var pipe=await Connect(deadline.Token);
            var attach=JsonSerializer.SerializeToUtf8Bytes(new{op="attach",proto=1,@short=job,attachId=id.ToString("N"),cols=120,rows=35,auth=Auth()});
            await pipe.WriteAsync(attach,deadline.Token);await pipe.WriteAsync(new byte[]{10},deadline.Token);await pipe.FlushAsync(deadline.Token);
            using var ack=await ReadReply(pipe,deadline.Token);
            var row=ack.RootElement;
            if(!row.TryGetProperty("ok",out var ok)||ok.ValueKind!=JsonValueKind.True||Str(row,"op")!="attach")return BeforeWrite("attach_not_acknowledged");
            // The job list can still show the previous "blocked" disposition
            // while the worker is generating. The attach ack's live tempo and
            // the worker's busy state must both agree on this turn.
            if(Str(row,"tempo")!="active")return BeforeWrite("attach_tempo_inactive");
            if(row.TryGetProperty("booting",out var boot)&&boot.ValueKind==JsonValueKind.True||Scope()!=scope||!Running())return BeforeWrite("attached_turn_changed");
            // The supervisor transmits these bytes only to this job's PTY.
            // A single ETX is never retried, even if the acknowledgement is lost.
            dispatch();written=true;
            await pipe.WriteAsync(new byte[]{3},deadline.Token);await pipe.FlushAsync(deadline.Token);
            while(!deadline.IsCancellationRequested){
                var observed=history();
                if(Scope()!=scope||observed.Limited)break;
                var turn=observed.Turns.LastOrDefault();
                if(turn==null||(string)turn["id"]!=target)break;
                if((string)turn["status"]=="interrupted"&&State=="idle")return new(false,true,null,"",Stopped:true);
                if((string)turn["status"] is "completed" or "failed")break;
                await Task.Delay(160,deadline.Token);
            }
        }catch(Exception ex) when(Expected(ex)||ex is KeyNotFoundException or OperationCanceledException){}
        return new(false,written,null,written?"delivery_unknown":"native_session_api_unavailable");
    }
    private static async Task<JsonDocument> ReadReply(Stream pipe,CancellationToken ct){
        var buffer=new byte[4096];int used=0;
        while(used<buffer.Length){
            int read=await pipe.ReadAsync(buffer.AsMemory(used),ct);
            if(read==0)break;
            int end=Array.IndexOf(buffer,(byte)10,used,read);
            if(end>=0)return JsonDocument.Parse(buffer.AsMemory(0,end));
            used+=read;
        }
        throw new IOException("native_background_reply_invalid");
    }
}
