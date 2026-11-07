using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace AIBotBridge;

// Existing interactive console only. Helpers are this bridge executable, not
// another Claude, shell, resume worker or permission owner.
internal sealed class ClaudeNativeTerminal:IClaudeNativeSession
{
    public ClaudeSessionSource Source=>ClaudeSessionSource.Terminal;
    internal readonly Guid Session;internal readonly int Pid;private readonly string _start,_record;
    private static readonly object ProbeLock=new();
    private static readonly Dictionary<int,(string Start,long Expires,bool Available)> Probes=[];
    private ClaudeNativeTerminal(Guid session,int pid,string start,string record){Session=session;Pid=pid;_start=start;_record=record;}
    internal static HashSet<Guid> LiveSessions(){
        var result=new HashSet<Guid>();string root=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","sessions");
        if(!Directory.Exists(root))return result;
        foreach(var path in Directory.EnumerateFiles(root,"*.json").Take(2048))try{using var doc=JsonDocument.Parse(File.ReadAllBytes(path));var r=doc.RootElement;if(ClaudeHistory.Text(r,"entrypoint")=="cli"&&Guid.TryParse(ClaudeHistory.Text(r,"sessionId"),out var id)&&Find(id) is not null)result.Add(id);}catch(Exception e) when(e is IOException or JsonException or UnauthorizedAccessException){}
        return result;
    }
    internal static ClaudeNativeTerminal? Find(Guid session){
        string root=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","sessions");if(!Directory.Exists(root))return null;
        var found=new List<ClaudeNativeTerminal>();
        foreach(string path in Directory.EnumerateFiles(root,"*.json").Take(2048))try {
            var info=new FileInfo(path);if(info.Length>16384||(info.Attributes&FileAttributes.ReparsePoint)!=0)continue;
            using var doc=JsonDocument.Parse(File.ReadAllBytes(path));var row=doc.RootElement;
            if(!Guid.TryParse(ClaudeHistory.Text(row,"sessionId"),out var id)||id!=session||!Supported(row))continue;
            if(!int.TryParse(Path.GetFileNameWithoutExtension(path),out int pid)||!row.TryGetProperty("pid",out var saved)||!saved.TryGetInt32(out var actual)||pid!=actual)continue;
            var client=new ClaudeNativeTerminal(session,pid,ClaudeHistory.Text(row,"procStart")??"",path);_=client.Scope();found.Add(client);
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){}
        return found.Count==1?found[0]:null;
    }
    private static bool Supported(JsonElement row)=>ClaudeHistory.Text(row,"entrypoint")=="cli"&&ClaudeHistory.Text(row,"kind")=="interactive"&&
        string.IsNullOrEmpty(ClaudeHistory.Text(row,"jobId"))&&ClaudeHistory.Text(row,"version") is "2.1.318" or "2.1.319";
    public string State {
        get {using var doc=JsonDocument.Parse(File.ReadAllBytes(_record));var r=doc.RootElement;using var p=Process.GetProcessById(Pid);
            string program=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile),".local","bin","claude.exe");
            if(p.HasExited||p.ProcessName!="claude"||!string.Equals(p.MainModule?.FileName,program,StringComparison.OrdinalIgnoreCase)||
                p.StartTime.ToUniversalTime().ToFileTimeUtc().ToString(CultureInfo.InvariantCulture)!=_start||!r.TryGetProperty("pid",out var pid)||!pid.TryGetInt32(out int actual)||actual!=Pid||
                !Guid.TryParse(ClaudeHistory.Text(r,"sessionId"),out var session)||session!=Session||ClaudeHistory.Text(r,"procStart")!=_start||!Supported(r))throw new IOException("native_session_changed");
            return ClaudeHistory.Text(r,"status")??"unknown";}
    }
    public string Scope(){_=State;return ClaudeNativeContent.Hash(System.Text.Encoding.UTF8.GetBytes(Session+":"+Pid+":"+_start+":console"));}
    public bool CanImages=>true;
    public bool UsesFileImageReferences=>true;
    // Acceptance uses the same adapter and capabilities as ordinary sessions;
    // the supplied scope only restricts which test process it can address.
    internal static ClaudeNativeTerminal? ForControlAcceptance(Guid session,string scope){
        var terminal=Find(session);
        if(terminal is null||terminal.Scope()!=scope)return null;
        return terminal;
    }
    public bool CanAppend=>true;
    public bool CanInterrupt=>true;
    internal bool RunningTurn(Guid user){
        string projectRoot=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","projects");
        byte[] key=new byte[32]; // Local comparison only. Device aliases use the private workspace key.
        var history=ClaudeHistory.Read(projectRoot,Session,key);
        var latest=history.Turns.LastOrDefault();
        return !history.Limited&&history.Accepted.Contains(user.ToString())&&latest is not null&&
            (string)latest["id"]==ClaudeCatalog.Alias("claude:turn:"+Session+":"+user,key)&&
            latest["status"] is "unknown"&&State is "busy" or "working";
    }
    public bool CanAddressInput {
        get {
            long now=Environment.TickCount64;
            lock(ProbeLock)if(Probes.TryGetValue(Pid,out var previous)&&previous.Start==_start&&previous.Expires>now)return previous.Available;
            bool available;
            try{_=Call("exclusive");available=true;}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or InvalidOperationException){available=false;}
            lock(ProbeLock){
                foreach(int old in Probes.Where(p=>p.Value.Expires<=now).Select(p=>p.Key).ToArray())Probes.Remove(old);
                Probes[Pid]=(_start,now+10000,available);
            }
            return available;
        }
    }
    public bool IsVerifiedBlank(){
        if(State!="idle"||!CanAddressInput)return false;
        string root=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","projects");
        if(Directory.EnumerateFiles(root,Session+".jsonl",new EnumerationOptions{RecurseSubdirectories=true,AttributesToSkip=FileAttributes.ReparsePoint}).Any())return false;
        return Call("exclusive") is {Empty:true} or {SuggestionCandidate:true};
    }
    internal static IEnumerable<ClaudeSessionObservation> NewPanels(){
        foreach(var id in LiveSessions()){
            ClaudeSessionObservation? row=null;
            try{
                if(Find(id) is {} terminal&&terminal.IsVerifiedBlank()){
                    using var data=JsonDocument.Parse(File.ReadAllBytes(terminal._record));
                    row=new(id,DateTime.FromFileTimeUtc(long.Parse(terminal._start,CultureInfo.InvariantCulture)),ClaudeHistory.Text(data.RootElement,"name")??"Claude Code",ClaudeHistory.Text(data.RootElement,"cwd"));
                }
            }catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){}
            if(row is not null)yield return row;
        }
    }
    internal object Inspect(){var view=Call("read");return new{available=true,host="terminal",state=State,canAddressInput=CanAddressInput,draftEmpty=view.Empty,imageCount=view.Images,hasVisibleText=view.Text.Length>0,suggestionLike=view.SuggestionCandidate,dimSuggestion=view.DimSuggestion,cursorColumn=view.CursorColumn,scope=Scope()};}
    internal sealed record View(string Text,int Images,string Target,bool Empty,int CursorColumn=0,bool DimSuggestion=false,bool SuggestionCandidate=false,string WrappedText=""){
        // The CLI can hide one separating space at a visual word wrap. Only
        // the worker may confirm that space by measuring the editable length.
        public bool HasHiddenSpace(string expected){
            int wrap=WrappedText.IndexOf('\n');
            return wrap>0&&wrap==WrappedText.LastIndexOf('\n')&&Text.Length+1==expected.Length&&
                expected.Length<=300&&expected[0] is >= '!' and <= '~'&&
                expected.All(c=>c is >= ' ' and <= '~' or >= '\u3000' and <= '\u9fff' or >= '\uff01' and <= '\uff60')&&
                WrappedText.Replace('\n',' ')==expected;
        }
    }
    internal sealed record Ticket(Guid Session,string Scope,string Action,string Text,string? Path,string? Digest,View? Expected,long WriteDeadline,Guid? ParentUser=null);
    internal sealed class ConsoleFailure(string stage):IOException(stage) { }
    private View Call(string action,string text="",string? path=null,string? digest=null,View? expected=null,Guid? parentUser=null) {
        string executable=Environment.ProcessPath??throw new IOException("native_helper_unavailable");
        var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
        if(Path.GetFileNameWithoutExtension(executable).Equals("dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
        start.ArgumentList.Add("--claude-native-console");
        using var process=Process.Start(start)??throw new IOException("native_helper_unavailable");
        process.StandardInput.WriteLine(JsonSerializer.Serialize(new Ticket(Session,Scope(),action,text,path,digest,expected,Environment.TickCount64+10000,parentUser)));process.StandardInput.Close();
        var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        bool ownsClipboard=action=="image";
        if(!process.WaitForExit(ownsClipboard?40000:10000)){
            // An image worker unwinds its own clipboard lease. Forcibly killing
            // it here would strand the temporary photo on the user's clipboard.
            if(!ownsClipboard)process.Kill();
            throw new ConsoleFailure("native_helper_timeout");
        }
        string body=output.GetAwaiter().GetResult();_=error.GetAwaiter().GetResult();
        if(body.Length>32000)throw new IOException("native_console_unconfirmed");
        if(process.ExitCode!=0){
            try{using var errorDoc=JsonDocument.Parse(body);string reason=ClaudeHistory.Text(errorDoc.RootElement,"error")??"";
                if(reason.Length<=80&&reason.StartsWith("native_console_",StringComparison.Ordinal)&&reason.All(c=>char.IsAsciiLetter(c)||c=='_'))throw new ConsoleFailure(reason);
            }catch(JsonException){}
            throw new IOException("native_console_unconfirmed");
        }
        return JsonSerializer.Deserialize<View>(body)??throw new IOException("native_console_unconfirmed");
    }
    public Task<ClaudeNativeSend.Result> SendAsync(string text,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token)=>ExecuteAsync(new("send",text,[],[],()=>true),history,dispatch,authorized,token);
    public async Task<ClaudeNativeSend.Result> ExecuteAsync(ClaudeNativeRequest request,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token){
        if(!authorized())return new(false,false,null,"unauthorized");
        if(!request.Current())return new(false,false,null,"turn_changed");
        bool append=request.Operation=="steer"&&CanAppend;
        bool stop=request.Operation=="interrupt"&&CanInterrupt;
        if(request.Operation!="send"&&!append&&!stop)return new(false,false,null,"native_operation_unavailable");
        if((append||stop)&&(request.ParentUser is not {} parent||!RunningTurn(parent)))return new(false,false,null,"turn_changed");
        string home=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude");
        if(File.Exists(Path.Combine(home,"keybindings.json")))return new(false,false,null,"native_custom_keybindings");
        if(stop)return await InterruptAsync(request,history,dispatch,authorized,token);
        if(request.Text.Any(char.IsControl)||request.Text.TrimStart().StartsWith('/')||request.Text.TrimStart().StartsWith('!')||
            request.Text.Length>2000||string.IsNullOrWhiteSpace(request.Text)&&request.Images.Length==0||
            request.Images.Length>Tab5CodexImages.MaxCount||request.Images.Length!=request.ImageDigests.Length)
            return new(false,false,null,"native_input_unsupported");
        string literal=request.Text;
        if(request.Images.Length>0){
            if(!request.Images.Select(ClaudeNativeContent.FileDigest).SequenceEqual(request.ImageDigests))return new(false,false,null,"native_image_changed");
            literal=string.Join(" ",request.Images.Select(ClaudeBackgroundApi.Reference))+(literal.Length>0?" "+literal:"");
        }
        if(literal.Length>8192)return new(false,false,null,"native_input_unsupported");
        ClaudeNativeSend.View Read(){
            var view=Call("exclusive");
            bool blank=view.Images==0&&(view.Empty||view.SuggestionCandidate);
            bool exact=view.Images==0&&!view.SuggestionCandidate&&(view.Text==literal||view.HasHiddenSpace(literal));
            bool ready=append?State is "busy" or "working":State=="idle";
            return new(Session,Scope(),view.Target,blank?"":exact?request.Text:view.Text,blank,ready,exact);
        }
        var calls=new ClaudeNativeSend.Calls(Read,Scope,
            _=>{
                var view=Call("exclusive");
                if(view.Images!=0||!view.Empty&&!view.SuggestionCandidate||!authorized()||!request.Current())throw new IOException("native_composer_changed");
                Call("foreground-type",literal,expected:view,parentUser:append?request.ParentUser:null);
            },
            ()=>{
                var view=Call("exclusive");
                if(!authorized()||!request.Current()||view.SuggestionCandidate||view.Images!=0||view.Text!=literal&&!view.HasHiddenSpace(literal))throw new IOException("native_composer_changed");
                Call("foreground-send",literal,expected:view,parentUser:append?request.ParentUser:null);
            },
            history,authorized,dispatch,ct=>Task.Delay(100,ct),request.ImageDigests,request.Current);
        return await ClaudeNativeSend.SendAsync(calls,request.Text,token);
    }
    private async Task<ClaudeNativeSend.Result> InterruptAsync(ClaudeNativeRequest request,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token){
        if(request.Text.Length!=0||request.Images.Length!=0||request.ParentUser is not {} parent)
            return new(false,false,null,"native_input_unsupported");
        var before=history();
        var turn=before.Turns.LastOrDefault();
        if(before.Limited||turn is null||!before.Accepted.Contains(parent.ToString()))return new(false,false,null,"turn_changed");
        string target=(string)turn["id"],scope=Scope();
        var input=Call("exclusive");
        if(!input.Empty&&!input.SuggestionCandidate||!authorized()||!request.Current())return new(false,false,null,"native_composer_changed");
        token.ThrowIfCancellationRequested();
        dispatch();
        try{
            Call("foreground-interrupt",expected:input,parentUser:parent);
            for(int i=0;i<70;i++){
                if(Scope()!=scope)break;
                var after=history();var latest=after.Turns.LastOrDefault();
                if(after.Limited||latest is null||(string)latest["id"]!=target||latest["status"] is "completed" or "failed")break;
                if(latest["status"] is "interrupted"&&State=="idle")return new(false,true,null,"",Stopped:true);
                await Task.Delay(150,token);
            }
        }catch(Exception ex) when(ex is IOException or OperationCanceledException or System.ComponentModel.Win32Exception){}
        return new(false,true,null,"delivery_unknown");
    }
}
