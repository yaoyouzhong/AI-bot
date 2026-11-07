using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

// A handle to the protocol stream owned by THIS extension child. Neither
// window handles nor the selected editor participate in writes or receipts.
internal sealed class ClaudeSessionApi(Guid session,int childPid,long childStart,int hostPid,long hostStart):IClaudeNativeSession
{
    public ClaudeSessionSource Source=>ClaudeSessionSource.VSCode;
    internal Guid Session=>session;
    internal static IEnumerable<ClaudeSessionObservation> NewPanels()=>ClaudeHarnessAdapters.Default.Discover();
    internal static IEnumerable<ClaudeSessionObservation> NewCodePanels(){
        foreach(var id in ClaudeNativeCode.LiveSessions()){
            var api=Find(id);
            if(api is not null&&api.IsFresh()){
                ClaudeSessionObservation? row=null;
                try{using var process=Process.GetProcessById(api.Child);using var record=Record(process);row=new(id,DateTime.FromFileTimeUtc(api.Start),"Claude Code",ClaudeHistory.Text(record.RootElement,"cwd"));}
                catch(Exception e) when(e is IOException or ArgumentException or InvalidOperationException or JsonException or UnauthorizedAccessException){}
                if(row is not null)yield return row;
            }
        }
        // Ordinary extension panels do not have a session API wrapper. Keep
        // their verified empty composers discoverable through the native path
        // used by Resolve as well. ClaudeCatalog deduplicates session IDs.
        foreach(var row in ClaudeNativeCode.NewPanels())yield return row;
    }
    private int Child=>childPid;
    private long Start=>childStart;
    internal static ClaudeSessionApi? Find(Guid id){
        string folder=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","sessions");
        if(!Directory.Exists(folder))return null;
        ClaudeSessionApi? found=null;int liveOwners=0;
        foreach(var file in Directory.EnumerateFiles(folder,"*.json").Take(2048)){
            if(!int.TryParse(Path.GetFileNameWithoutExtension(file),out int pid))continue;
            try{
                using var child=Process.GetProcessById(pid);
                if(child.ProcessName!="claude"||Metadata(child,id) is null)continue;
                if(++liveOwners!=1)return null;
                using var owner=Process.GetProcessById(ClaudeNativeCode.Parent(child));
                if(ClaudeSessionHost.SameHost(owner)){
                    _=ClaudeNativeCode.CodeRoot(child);
                    found=new(id,pid,child.StartTime.ToUniversalTime().ToFileTimeUtc(),owner.Id,owner.StartTime.ToUniversalTime().ToFileTimeUtc());
                }
            }catch(Exception e) when(e is ArgumentException or IOException or JsonException or InvalidOperationException or UnauthorizedAccessException or System.ComponentModel.Win32Exception){}
        }
        return found;
    }
    internal static IClaudeNativeSession? Resolve(Guid session){
        return ClaudeHarnessAdapters.Default.Resolve(session);
    }
    internal static string? Metadata(Process child,Guid expected){
        if(child.HasExited||child.ProcessName!="claude")return null;
        using var doc=Record(child);var row=doc.RootElement;
        if(ClaudeHistory.Text(row,"entrypoint")!="claude-vscode"||!Guid.TryParse(ClaudeHistory.Text(row,"sessionId"),out var sid)||sid!=expected||
            !row.TryGetProperty("pid",out var pid)||pid.GetInt32()!=child.Id||ClaudeHistory.Text(row,"procStart")!=child.StartTime.ToUniversalTime().ToFileTimeUtc().ToString(System.Globalization.CultureInfo.InvariantCulture))return null;
        return ClaudeHistory.Text(row,"status");
    }
    private static JsonDocument Record(Process child){
        string path=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","sessions",child.Id+".json");
        var info=new FileInfo(path);
        if(!info.Exists||info.Length>16384||(info.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("native_session_changed");
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        return JsonDocument.Parse(stream);
    }
    public string State{get{using var child=Process.GetProcessById(childPid);return Metadata(child,session)??throw new IOException("native_session_changed");}}
    public string Scope(){
        using var child=Process.GetProcessById(childPid);using var host=Process.GetProcessById(hostPid);
        if(child.StartTime.ToUniversalTime().ToFileTimeUtc()!=childStart||ClaudeNativeCode.Parent(child)!=host.Id||host.StartTime.ToUniversalTime().ToFileTimeUtc()!=hostStart||!ClaudeSessionHost.SameHost(host)||Metadata(child,session) is null)throw new IOException("native_session_changed");
        return ClaudeNativeContent.Hash(Encoding.UTF8.GetBytes($"api-v1:{session}:{childPid}:{childStart}:{hostPid}:{hostStart}"));
    }
    public bool HasSessionApi=>true;
    public bool CanImages=>true;
    public bool CanAppend=>true;
    public bool CanInterrupt=>true;
    private async Task<NamedPipeClientStream> Connect(CancellationToken token){
        _=Scope();using var child=Process.GetProcessById(childPid);
        var pipe=new NamedPipeClientStream(".",ClaudeSessionHost.PipeName(child),PipeDirection.InOut,PipeOptions.CurrentUserOnly|PipeOptions.Asynchronous);
        try{
            await pipe.ConnectAsync(1500,token);
            if(!ClaudeSessionHost.GetNamedPipeServerProcessId(pipe.SafePipeHandle,out int pid)||pid!=hostPid)throw new IOException("native_pipe_identity_changed");
            _=Scope();return pipe;
        }catch{pipe.Dispose();throw;}
    }
    public Task<ClaudeNativeSend.Result> SendAsync(string text,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token)
        =>ExecuteAsync(new("send",text,[],[],()=>true,Guid.NewGuid()),history,dispatch,authorized,token);
    public async Task<ClaudeNativeSend.Result> ExecuteAsync(ClaudeNativeRequest request,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token){
        if(request.Id is not {} id||!authorized()||!request.Current())return new(false,false,null,"turn_changed");
        using var limit=CancellationTokenSource.CreateLinkedTokenSource(token);limit.CancelAfter(TimeSpan.FromSeconds(20));
        bool written=false;
        try{
            if(request.Images.Length!=request.ImageDigests.Length||request.Images.Length>3)return new(false,false,null,"native_input_unsupported");
            var images=new List<string>();
            for(int i=0;i<request.Images.Length;i++){
                if(ClaudeNativeContent.FileDigest(request.Images[i])!=request.ImageDigests[i])return new(false,false,null,"native_image_changed");
                byte[] bytes=await File.ReadAllBytesAsync(request.Images[i],limit.Token);
                if(ClaudeNativeContent.ImageDigest(bytes)!=request.ImageDigests[i])return new(false,false,null,"native_image_changed");
                images.Add(Convert.ToBase64String(bytes));
            }
            using var pipe=await Connect(limit.Token);
            _=history();if(!authorized()||!request.Current())return new(false,false,null,"turn_changed");
            dispatch();written=true;
            await ClaudeSessionHost.WriteFrame(pipe,new ClaudeSessionHost.Command(session,id,request.Operation,request.ParentUser,request.Text,images.ToArray()),limit.Token);
            var reading=ClaudeSessionHost.ReadFrame(pipe,4096,limit.Token);
            while(request.Operation!="interrupt"&&!reading.IsCompleted){
                if(await Task.WhenAny(reading,Task.Delay(150,limit.Token))==reading)break;
                try{
                    var snapshot=history();
                    if(!snapshot.Limited&&snapshot.Accepted.Contains(id.ToString())&&snapshot.InputDigests.GetValueOrDefault(id.ToString())==ClaudeNativeContent.Digest(request.Text,request.ImageDigests)){
                        await limit.CancelAsync();
                        try{await reading;}catch(Exception e) when(e is IOException or OperationCanceledException){}
                        return new(true,true,id,"");
                    }
                }catch(IOException){}
                limit.Token.ThrowIfCancellationRequested();
            }
            var reply=(await reading).Deserialize<ClaudeSessionHost.Reply>();
            if(reply?.State=="rejected")return new(true,false,null,"turn_changed");
            if(request.Operation=="interrupt"&&reply?.State=="stopped")return new(false,true,null,"",Stopped:true);
            if(request.Operation!="interrupt"&&reply?.State=="accepted"&&reply.User==id)return new(true,true,id,"");
        }catch(Exception e) when(e is IOException or TimeoutException or OperationCanceledException or InvalidOperationException or JsonException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception){}
        return new(written,written,null,written?"delivery_unknown":"native_session_api_unavailable");
    }
    internal bool IsFresh()=>Query(Guid.NewGuid(),"fresh")?.State=="fresh";
    public bool IsVerifiedBlank()=>IsFresh();
    internal bool IsRunning(Guid raw)=>Query(Guid.NewGuid(),"active",raw)?.State=="active";
    internal ClaudeSessionHost.Reply? Receipt(Guid id)=>Query(id,"receipt");
    private ClaudeSessionHost.Reply? Query(Guid id,string operation,Guid? parent=null){
        using var limit=new CancellationTokenSource(TimeSpan.FromSeconds(2));
        try{
            using var pipe=Connect(limit.Token).GetAwaiter().GetResult();
            ClaudeSessionHost.WriteFrame(pipe,new ClaudeSessionHost.Command(session,id,operation,parent,"",[]),limit.Token).GetAwaiter().GetResult();
            return ClaudeSessionHost.ReadFrame(pipe,4096,limit.Token).GetAwaiter().GetResult().Deserialize<ClaudeSessionHost.Reply>();
        }catch(Exception e) when(e is IOException or TimeoutException or OperationCanceledException or InvalidOperationException or JsonException){return null;}
    }
}
