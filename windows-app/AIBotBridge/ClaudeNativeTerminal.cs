using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

namespace AIBotBridge;

// Existing interactive console only. Helpers are this bridge executable, not
// another Claude, shell, resume worker or permission owner.
internal sealed class ClaudeNativeTerminal:IClaudeNativeSession
{
    public ClaudeSessionSource Source=>ClaudeSessionSource.Terminal;
    internal readonly Guid Session;internal readonly int Pid;private readonly string _start,_record;
    private static readonly object ProbeLock=new();
    private static readonly Dictionary<int,(string Start,long Expires,bool Available)> Probes=[];
    internal static void ClearProbes(){lock(ProbeLock)Probes.Clear();}
    private ClaudeNativeTerminal(Guid session,int pid,string start,string record){Session=session;Pid=pid;_start=start;_record=record;}
    internal static HashSet<Guid> LiveSessions()=>Candidates().GroupBy(c=>c.Session).Where(g=>g.Count()==1).Select(g=>g.Key).ToHashSet();
    internal static ClaudeNativeTerminal? Find(Guid session){var found=Candidates(session);return found.Count==1?found[0]:null;}
    private static List<ClaudeNativeTerminal> Candidates(Guid? session=null,IReadOnlySet<Guid>? known=null){
        string root=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","sessions");if(!Directory.Exists(root))return [];
        var found=new List<ClaudeNativeTerminal>();
        foreach(string path in Directory.EnumerateFiles(root,"*.json").Take(2048))try {
            var info=new FileInfo(path);if(info.Length>16384||(info.Attributes&FileAttributes.ReparsePoint)!=0)continue;
            using var doc=JsonDocument.Parse(File.ReadAllBytes(path));var row=doc.RootElement;
            if(!Guid.TryParse(ClaudeHistory.Text(row,"sessionId"),out var id)||session is {} target&&id!=target||known?.Contains(id)==true||!Supported(row))continue;
            if(!int.TryParse(Path.GetFileNameWithoutExtension(path),out int pid)||!row.TryGetProperty("pid",out var saved)||!saved.TryGetInt32(out var actual)||pid!=actual)continue;
            var client=new ClaudeNativeTerminal(id,pid,ClaudeHistory.Text(row,"procStart")??"",path);_=client.Scope();found.Add(client);
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){}
        return found;
    }
    private static bool Supported(JsonElement row)=>ClaudeHistory.Text(row,"entrypoint")=="cli"&&ClaudeHistory.Text(row,"kind")=="interactive"&&
        string.IsNullOrEmpty(ClaudeHistory.Text(row,"jobId"))&&ClaudeHistory.Text(row,"version") is "2.1.318" or "2.1.319";
    public string State {
        get {using var doc=JsonDocument.Parse(File.ReadAllBytes(_record));var r=doc.RootElement;
            string program=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile),".local","bin","claude.exe");
            if(!ProcessIdentityMatches(Pid,program,_start)||!r.TryGetProperty("pid",out var pid)||!pid.TryGetInt32(out int actual)||actual!=Pid||
                !Guid.TryParse(ClaudeHistory.Text(r,"sessionId"),out var session)||session!=Session||ClaudeHistory.Text(r,"procStart")!=_start||!Supported(r))throw new IOException("native_session_changed");
            return ClaudeHistory.Text(r,"status")??"unknown";}
    }
    // Reading an elevated process's MainModule requires VM_READ. The image,
    // creation time and exit status can all be checked on one limited-query
    // handle; this identifies a source without granting console input access.
    internal static bool ProcessIdentityMatches(int pid,string expectedImage,string expectedStart){
        using var process=OpenProcess(0x1000,false,pid);
        if(process.IsInvalid)return false;
        var path=new StringBuilder(32768);int length=path.Capacity;
        return QueryFullProcessImageName(process,0,path,ref length)&&
            string.Equals(path.ToString(),expectedImage,StringComparison.OrdinalIgnoreCase)&&
            GetProcessTimes(process,out long created,out long exited,out _,out _) && exited==0&&
            created.ToString(CultureInfo.InvariantCulture)==expectedStart&&
            GetExitCodeProcess(process,out uint exitCode)&&exitCode==259;
    }
    [DllImport("kernel32.dll",SetLastError=true)]
    private static extern SafeProcessHandle OpenProcess(uint access,[MarshalAs(UnmanagedType.Bool)]bool inherit,int id);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process,int flags,StringBuilder name,ref int length);
    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetProcessTimes(SafeProcessHandle process,out long creation,out long exit,out long kernel,out long user);
    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetExitCodeProcess(SafeProcessHandle process,out uint exitCode);
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
                // A console can be unavailable briefly while its original
                // session is resuming. Don't reuse a failed probe for 10 s.
                Probes[Pid]=(_start,available?now+10000:Environment.TickCount64+750,available);
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
    internal static IEnumerable<ClaudeSessionObservation> NewPanels(IReadOnlySet<Guid>? known=null){
        foreach(var group in Candidates(known:known).GroupBy(c=>c.Session).Where(g=>g.Count()==1)){
            var terminal=group.Single();var id=terminal.Session;
            ClaudeSessionObservation? row=null;
            try{
                if(terminal.IsVerifiedBlank()){
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
    private View Call(string action,string text="",string? path=null,string? digest=null,View? expected=null,Guid? parentUser=null,ConsoleWorker? worker=null) {
        bool wrapped=action is "foreground-send" or "foreground-verify"&&expected?.HasHiddenSpace(text)==true;
        var ticket=new Ticket(Session,Scope(),action,text,path,digest,expected,Environment.TickCount64+(wrapped?30000:10000),parentUser);
        if(ClaudeNativeConsole.Integrity((uint)Pid)>ClaudeNativeConsole.Integrity((uint)Environment.ProcessId))
            throw new ConsoleFailure("native_console_elevation_required_unconfirmed");
        if(worker is not null)return worker.Call(ticket);
        using var once=new ConsoleWorker();return once.Call(ticket);
    }
    private sealed class ConsoleWorker:IDisposable {
        private Process? _process;private bool _failed,_clipboard;
        internal View Call(Ticket ticket){
            if(_failed)throw new IOException("native_console_unconfirmed");
            if(_process is null){
                string executable=Environment.ProcessPath??throw new IOException("native_helper_unavailable");
                var start=new ProcessStartInfo(executable){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true};
                if(Path.GetFileNameWithoutExtension(executable).Equals("dotnet",StringComparison.OrdinalIgnoreCase))start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
                start.ArgumentList.Add("--claude-native-console");
                _process=Process.Start(start)??throw new IOException("native_helper_unavailable");
                _=_process.StandardError.ReadToEndAsync();
            }
            _clipboard=ticket.Action=="image";
            try{
                // Exactly one write. A timeout or invalid response retires the
                // channel; it never restarts a worker or replays the command.
                _process.StandardInput.WriteLine(JsonSerializer.Serialize(ticket));_process.StandardInput.Flush();
                bool wrapped=ticket.Action is "foreground-send" or "foreground-verify"&&ticket.Expected?.HasHiddenSpace(ticket.Text)==true;
                string body=_process.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(_clipboard?40:wrapped?35:10)).GetAwaiter().GetResult()??throw new IOException("native_console_unconfirmed");
                if(body.Length>32000)throw new IOException("native_console_unconfirmed");
                using var response=JsonDocument.Parse(body);
                if(response.RootElement.TryGetProperty("error",out var error)){
                    string reason=error.GetString()??"";
                    if(reason.Length<=80&&reason.StartsWith("native_console_",StringComparison.Ordinal)&&reason.All(c=>char.IsAsciiLetter(c)||c=='_'))throw new ConsoleFailure(reason);
                    throw new IOException("native_console_unconfirmed");
                }
                var view=JsonSerializer.Deserialize<View>(body)??throw new IOException("native_console_unconfirmed");
                _clipboard=false;return view;
            }catch(TimeoutException){_failed=true;throw new ConsoleFailure("native_helper_timeout");}
            catch{_failed=true;throw;}
        }
        public void Dispose(){
            if(_process is null)return;
            try{
                _process.StandardInput.Close();
                // A clipboard worker must unwind its lease itself on failure.
                if(!_process.WaitForExit(_clipboard?100:2000)&&!_clipboard)_process.Kill();
            }catch(Exception e) when(e is IOException or InvalidOperationException or System.ComponentModel.Win32Exception){}
            finally{_process.Dispose();}
        }
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
        using var worker=new ConsoleWorker();
        ClaudeNativeSend.View Read(){
            var view=Call("exclusive",worker:worker);
            bool blank=view.Images==0&&(view.Empty||view.SuggestionCandidate);
            bool exact=view.Images==0&&!view.SuggestionCandidate&&(view.Text==literal||view.HasHiddenSpace(literal));
            bool ready=append?State is "busy" or "working":State=="idle";
            return new(Session,Scope(),view.Target,blank?"":exact?request.Text:view.Text,blank,ready,exact);
        }
        var calls=new ClaudeNativeSend.Calls(Read,Scope,
            _=>{
                var view=Call("exclusive",worker:worker);
                if(view.Images!=0||!view.Empty&&!view.SuggestionCandidate||!authorized()||!request.Current())throw new IOException("native_composer_changed");
                Call("foreground-type",literal,expected:view,parentUser:append?request.ParentUser:null,worker:worker);
            },
            ()=>{
                var view=Call("exclusive",worker:worker);
                if(!authorized()||!request.Current()||view.SuggestionCandidate||view.Images!=0||view.Text!=literal&&!view.HasHiddenSpace(literal))throw new IOException("native_composer_changed");
                Call("foreground-send",literal,expected:view,parentUser:append?request.ParentUser:null,worker:worker);
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
