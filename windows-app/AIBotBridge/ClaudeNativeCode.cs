using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;

namespace AIBotBridge;

// Adapter for an existing native VS Code Claude session, never a new CLI owner.
// Inspection is read-only. Activation/input require an explicit authorized send.
internal sealed partial class ClaudeNativeCode:IClaudeNativeSession
{
    public ClaudeSessionSource Source=>ClaudeSessionSource.VSCode;
    private readonly Guid _session;private readonly int _pid,_codePid;
    private readonly string _start,_directory,_database,_record,_codeStart;
    private sealed record View(nint Window,AutomationElement Frame,AutomationElement Editor,AutomationElement Button,string Text,bool Empty,AutomationElement Composer,string[] Images,bool Hint=false);
    private ClaudeNativeCode(Guid session,int pid,string start,string directory,int codePid,string codeStart,string database,string record){
        _session=session;_pid=pid;_start=start;_directory=directory;_codePid=codePid;_codeStart=codeStart;_database=database;_record=record;
    }
    private static void Require(bool value,string reason){if(!value)throw new IOException(reason);}
    private static string Text(JsonElement value,string key)=>ClaudeHistory.Text(value,key)??"";
    private static JsonElement Property(JsonElement value,string key)=>value.ValueKind==JsonValueKind.Object&&value.TryGetProperty(key,out var child)?child:throw new IOException("native_state_unconfirmed");
    private static byte[] Regular(string path,int limit){
        Require((File.GetAttributes(path)&FileAttributes.ReparsePoint)==0&&(File.GetAttributes(Path.GetDirectoryName(path)!)&FileAttributes.ReparsePoint)==0,"native_identity_unconfirmed");
        using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        Require(file.Length is >0&&file.Length<=limit,"native_identity_unconfirmed");var bytes=new byte[(int)file.Length];file.ReadExactly(bytes);return bytes;
    }
    private static string Start(Process process)=>process.StartTime.ToUniversalTime().ToFileTimeUtc().ToString(CultureInfo.InvariantCulture);
    internal int ChildPid=>_pid;
    internal Guid SessionId=>_session;
    internal static int Parent(Process process){Require(NtQueryInformationProcess(process.Handle,0,out var info,Marshal.SizeOf<BasicInfo>(),out _)==0,"native_host_unconfirmed");return checked((int)info.Parent);}
    internal static int CodeRoot(Process native){
        int current=Parent(native),last=0;
        DateTime childStarted=native.StartTime.ToUniversalTime();
        for(int depth=0;depth<8;depth++){
            Process process;
            try{process=Process.GetProcessById(current);}
            catch(ArgumentException) when(last!=0){return last;} // VS Code can outlive its launcher.
            using(process){
                var started=process.StartTime.ToUniversalTime();
                if(depth==0&&started<=childStarted&&ClaudeSessionHost.SameHost(process)){current=Parent(process);childStarted=started;continue;}
                if(process.ProcessName!="Code"||started>childStarted){Require(last!=0,"native_host_unconfirmed");return last;}
                last=process.Id;current=Parent(process);childStarted=started;
            }
        }
        throw new IOException("native_host_unconfirmed");
    }
    internal static ClaudeNativeCode? Find(Guid session,bool explain=false)
    {
        if(session==Guid.Empty)return null;
        try {
            string directory=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","sessions");
            Require(Directory.Exists(directory),"native_catalog_unavailable");
            var matches=new List<(int Pid,string Start,string Cwd,string Path)>();int count=0;
            foreach(string path in Directory.EnumerateFiles(directory,"*.json")){
                if(++count>2048)throw new IOException("native_catalog_unconfirmed");
                try {
                    using var doc=JsonDocument.Parse(Regular(path,16384));var row=doc.RootElement;
                    if(!Guid.TryParse(Text(row,"sessionId"),out var id)||id!=session)continue;
                    Require(Text(row,"entrypoint")=="claude-vscode"&&Text(row,"kind")=="interactive","native_client_unsupported");
                    Require(int.TryParse(Path.GetFileNameWithoutExtension(path),out int pid)&&pid>0&&Property(row,"pid").TryGetInt32(out int saved)&&saved==pid,"native_identity_unconfirmed");
                    using var process=Process.GetProcessById(pid);string start=Text(row,"procStart"),cwd=Text(row,"cwd");
                    Require(process.ProcessName=="claude"&&!process.HasExited&&Start(process)==start&&Path.IsPathFullyQualified(cwd),"native_identity_unconfirmed");
                    matches.Add((pid,start,Path.GetFullPath(cwd),path));
                }catch(Exception e) when(e is JsonException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){ }
            }
            Require(matches.Count==1,matches.Count==0?"native_process_unavailable":"native_process_ambiguous");var match=matches[0];
            using var native=Process.GetProcessById(match.Pid);int code=CodeRoot(native);using var host=Process.GetProcessById(code);
            string storage=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Code","User","workspaceStorage");
            Require(Directory.Exists(storage),"native_workspace_storage_unavailable");var databases=new List<string>();count=0;
            foreach(string folder in Directory.EnumerateDirectories(storage)){
                if(++count>2048)throw new IOException("native_catalog_unconfirmed");
                if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0)continue;
                string db=Path.Combine(folder,"state.vscdb");if(!File.Exists(db))continue;
                try{using var doc=JsonDocument.Parse(ReadDatabase(db,"Anthropic.claude-code"));
                    if(doc.RootElement.TryGetProperty("panelTabSessions",out var panels)&&panels.ValueKind==JsonValueKind.Array&&
                        panels.EnumerateArray().Any(p=>Guid.TryParse(Text(p,"sessionId"),out var id)&&id==session))databases.Add(db);
                }catch(Exception e) when(e is IOException or JsonException){ }
            }
            Require(databases.Count==1,databases.Count==0?"native_panel_binding_unavailable":"native_panel_binding_ambiguous");
            return new(session,match.Pid,match.Start,match.Cwd,code,Start(host),databases[0],match.Path);
        }catch(Exception e) when(!explain&&(e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)){return null;}
    }
    internal static HashSet<Guid> LiveSessions(){
        var ids=new HashSet<Guid>();string root=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","sessions");
        if(!Directory.Exists(root))return ids;int count=0;
        foreach(string path in Directory.EnumerateFiles(root,"*.json")){
            if(++count>2048)break;
            try{using var doc=JsonDocument.Parse(Regular(path,16384));var row=doc.RootElement;
                if(Text(row,"entrypoint")!="claude-vscode"||!Guid.TryParse(Text(row,"sessionId"),out var id)||!int.TryParse(Path.GetFileNameWithoutExtension(path),out int pid))continue;
                using var process=Process.GetProcessById(pid);if(process.ProcessName=="claude"&&!process.HasExited&&Start(process)==Text(row,"procStart"))ids.Add(id);
            }catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){}
        }
        return ids;
    }
    private string CheckNative(bool idle=false){
        using var doc=JsonDocument.Parse(Regular(_record,16384));var row=doc.RootElement;
        Require(Guid.TryParse(Text(row,"sessionId"),out var id)&&id==_session&&Text(row,"procStart")==_start&&Text(row,"entrypoint")=="claude-vscode"&&
            string.Equals(Path.GetFullPath(Text(row,"cwd")),_directory,StringComparison.OrdinalIgnoreCase),"native_session_changed");
        using var native=Process.GetProcessById(_pid);using var host=Process.GetProcessById(_codePid);
        Require(native.ProcessName=="claude"&&!native.HasExited&&Start(native)==_start&&CodeRoot(native)==_codePid&&host.ProcessName=="Code"&&Start(host)==_codeStart,"native_session_changed");
        string state=Text(row,"status");if(idle)Require(state=="idle","native_session_busy");return state;
    }
    internal string Scope(){_=CheckNative();return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_session+":"+_pid+":"+_start+":"+_codePid+":"+_codeStart+":"+_database)));}
    string IClaudeNativeSession.State=>CheckNative();
    bool IClaudeNativeSession.CanAppend=>true;
    bool IClaudeNativeSession.CanInterrupt=>true;
    bool IClaudeNativeSession.CanImages=>true;
    public bool CanAddressInput {
        get {try{_=Read(idle:false);return true;}
            catch(Exception e) when(e is IOException or InvalidOperationException or System.Runtime.InteropServices.COMException or System.Windows.Automation.ElementNotAvailableException){return false;}}
    }
    public bool IsVerifiedBlank(){
        try{return IsNewPanel()&&Read().Empty;}
        catch(Exception e) when(e is IOException or InvalidOperationException or System.Runtime.InteropServices.COMException or System.Windows.Automation.ElementNotAvailableException){return false;}
    }
    string IClaudeNativeSession.Scope()=>Scope();
    Task<ClaudeNativeSend.Result> IClaudeNativeSession.SendAsync(string text,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token)=>SendAsync(text,history,dispatch,authorized,token);
    private bool CheckSelected(){
        using var extension=JsonDocument.Parse(ReadDatabase(_database,"Anthropic.claude-code"));
        var panels=Property(extension.RootElement,"panelTabSessions");
        Require(panels.EnumerateArray().Count(p=>Guid.TryParse(Text(p,"sessionId"),out var id)&&id==_session)==1,"native_session_ambiguous");
        using var doc=JsonDocument.Parse(ReadDatabase(_database,"memento/workbench.parts.editor"));var part=Property(doc.RootElement,"editorpart.state");
        var groups=new List<JsonElement>();
        void Visit(JsonElement node){if(Text(node,"type")=="leaf")groups.Add(Property(node,"data"));else foreach(var child in Property(node,"data").EnumerateArray())Visit(child);}
        Visit(Property(Property(part,"serializedGrid"),"root"));
        var selected=groups.Where(g=>Property(g,"id").GetInt32()==Property(part,"activeGroup").GetInt32()).ToArray();Require(selected.Length==1,"native_editor_ambiguous");
        var group=selected[0];var mru=Property(group,"mru");var editors=Property(group,"editors");
        Require(mru.GetArrayLength()>0,"native_editor_unconfirmed");int index=mru[0].GetInt32();Require(index>=0&&index<editors.GetArrayLength(),"native_editor_unconfirmed");
        using var tab=JsonDocument.Parse(Text(editors[index],"value"));
        Require(Text(tab.RootElement,"extensionId")=="Anthropic.claude-code"&&Text(tab.RootElement,"viewType")=="mainThreadWebview-claudeVSCodePanel","native_editor_unconfirmed");
        using var state=JsonDocument.Parse(Text(tab.RootElement,"state"));Require(Guid.TryParse(Text(state.RootElement,"sessionID"),out var session)&&session==_session,"native_selected_session_changed");
        return Guid.TryParse(Text(state.RootElement,"sessionWithNoTranscript"),out var fresh)&&fresh==_session;
    }
    internal bool IsNewPanel(){
        try{return CheckNative()=="idle"&&CheckSelected();}
        catch(Exception e) when(e is IOException or JsonException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException){return false;}
    }
    internal static IEnumerable<ClaudeSessionObservation> NewPanels(IReadOnlySet<Guid>? known=null){
        foreach(var id in LiveSessions()){
            if(known?.Contains(id)==true)continue;
            var code=Find(id);if(code is null)continue;
            ClaudeSessionObservation? observation=null;
            try{
                if(code.IsNewPanel())
                    observation=new(id,DateTime.FromFileTimeUtc(long.Parse(code._start,CultureInfo.InvariantCulture)),"Claude Code",code._directory);
            }catch(Exception e) when(e is IOException or JsonException or InvalidOperationException or System.ComponentModel.Win32Exception or UnauthorizedAccessException){ }
            if(observation is not null)yield return observation;
        }
    }
    private View Read(bool idle=true){
        string state=CheckNative(idle);Require(state is "idle" or "busy" or "working","native_session_waiting");CheckSelected();
        var frames=new List<(nint Window,AutomationElement Frame)>();
        EnumWindows((window,_)=>{GetWindowThreadProcessId(window,out uint pid);if(pid!=(uint)_codePid||!IsWindowVisible(window))return true;
            var root=AutomationElement.FromHandle(window);
            foreach(AutomationElement frame in root.FindAll(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Document),new PropertyCondition(AutomationElement.AutomationIdProperty,"active-frame"))))if(!frame.Current.IsOffscreen)frames.Add((window,frame));return true;},0);
        Require(frames.Count==1,"native_frame_ambiguous");var selected=frames[0];
        AutomationElement[] Find(ControlType type,string name)=>selected.Frame.FindAll(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,type),new PropertyCondition(AutomationElement.NameProperty,name))).Cast<AutomationElement>().Where(e=>!e.Current.IsOffscreen).ToArray();
        var editors=Find(ControlType.Edit,"Message input");var buttons=Find(ControlType.Button,"Send message").Concat(Find(ControlType.Button,"Stop")).ToArray();Require(editors.Length==1&&buttons.Length==1,"native_composer_unconfirmed");
        var editor=editors[0];var button=buttons[0];Require(editor.Current.IsEnabled&&editor.TryGetCurrentPattern(ValuePattern.Pattern,out _)&&button.TryGetCurrentPattern(InvokePattern.Pattern,out _),"native_composer_unconfirmed");
        var value=(ValuePattern)editor.GetCurrentPattern(ValuePattern.Pattern);Require(!value.Current.IsReadOnly,"native_composer_unconfirmed");string text=value.Current.Value;
        var leaves=editor.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Text)).Cast<AutomationElement>().ToArray();
        bool singleLeaf=text.Length>0&&leaves.Length==1&&leaves[0].Current.Name==text;
        bool readOnlyHint=false;
        if(singleLeaf&&editor.TryGetCurrentPattern(TextPattern.Pattern,out var pattern)){
            var range=((TextPattern)pattern).DocumentRange;
            readOnlyHint=range.GetText(-1)==text && range.GetAttributeValue(TextPattern.IsReadOnlyAttribute) is true;
        }
        bool placeholder=singleLeaf&&(KnownHint(text)||readOnlyHint);
        var composer=TreeWalker.ControlViewWalker.GetParent(editor);bool found=false;
        for(int depth=0;composer is not null&&depth<8;depth++,composer=TreeWalker.ControlViewWalker.GetParent(composer)) {
            if(composer.GetRuntimeId().SequenceEqual(selected.Frame.GetRuntimeId()))break;
            if(composer.FindAll(TreeScope.Descendants,new AndCondition(new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button),new PropertyCondition(AutomationElement.NameProperty,"Add"))).Count==1){found=true;break;}
        }
        Require(found&&composer is not null,"native_composer_boundary_unconfirmed");
        var images=AttachmentNames(composer!);
        bool empty=images.Length==0&&(text.Length==0||placeholder)&&(!button.Current.IsEnabled||button.Current.Name=="Stop");
        _=CheckNative(idle);CheckSelected();return new(selected.Window,selected.Frame,editor,button,empty||placeholder&&images.Length>0?"":text,empty,composer!,images,placeholder);
    }
    internal static bool KnownHint(string text)=>text is "ctrl esc to focus or unfocus Claude" or "ctrl esc to attach selected text" or "Ask Claude to edit…" or "Queue another message…";
    internal static string[] AttachmentNames(AutomationElement composer){
        // The extension renders SVG toolbar icons as UIA images too. Its actual
        // draft attachments are pills containing a label and Remove attachment.
        var remove=composer.FindAll(TreeScope.Descendants,new AndCondition(
            new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button),
            new PropertyCondition(AutomationElement.NameProperty,"Remove attachment")))
            .Cast<AutomationElement>().Where(e=>!e.Current.IsOffscreen).ToArray();
        Require(remove.Length<=3,"native_attachments_unconfirmed");
        var names=new List<string>();
        foreach(var button in remove){
            var pill=TreeWalker.RawViewWalker.GetParent(button);
            string? name=null;
            for(int depth=0;pill is not null&&depth<6;depth++,pill=TreeWalker.RawViewWalker.GetParent(pill)){
                if(pill.GetRuntimeId().SequenceEqual(composer.GetRuntimeId()))break;
                if(pill.Current.ControlType!=ControlType.Button)continue;
                name=AttachmentName(pill.Current.Name);
                if(name is null)continue;
                var controls=pill.FindAll(TreeScope.Descendants,new PropertyCondition(
                    AutomationElement.NameProperty,"Remove attachment"));
                Require(controls.Count==1,"native_attachment_pill_ambiguous");
                break;
            }
            Require(name is {Length:>0 and <=255},"native_attachment_label_unavailable");
            names.Add(name!);
        }
        return names.ToArray();
    }
    internal static string? AttachmentName(string label){
        const string suffix=" Remove attachment";
        if(label.Length>320||!label.EndsWith(suffix,StringComparison.Ordinal))return null;
        string content=label[..^suffix.Length];
        int separator=content.LastIndexOf(' ');
        if(separator<=0||separator>255)return null;
        var dimensions=content[(separator+1)..].Split('×');
        if(dimensions.Length!=2||!int.TryParse(dimensions[0],out int width)||width<=0||
            !int.TryParse(dimensions[1],out int height)||height<=0)return null;
        return content[..separator];
    }
    internal object Inspect(){var view=Read(false);return new{available=true,host="vscode",state=CheckNative(),draftEmpty=view.Empty,nativeSubmitAvailable=true,button=view.Button.Current.Name,buttonEnabled=view.Button.Current.IsEnabled,hint=view.Hint,imageCount=view.Images.Length,hasUnclassifiedText=view.Text.Length>0&&!view.Hint,scope=Scope()};}
    private static bool Focused(View view)=>GetForegroundWindow()==view.Window&&view.Editor.Current.HasKeyboardFocus&&
        AutomationElement.FocusedElement is {} focused&&focused.GetRuntimeId().SequenceEqual(view.Editor.GetRuntimeId());
    private static Task<bool> RestoreForInputAsync(nint window,Func<bool> current,CancellationToken token)=>
        ClaudeNativeFocusPreparation.RestoreAsync(()=>IsIconic(window),()=>ShowWindowAsync(window,9),
            ()=>current()&&Tab5DesktopActivation.IsInteractive()&&!Held.Any(k=>(GetAsyncKeyState(k)&0x8000)!=0),token);
    [DllImport("user32.dll")]private static extern bool IsIconic(nint window);
    [DllImport("user32.dll")]private static extern bool ShowWindowAsync(nint window,int command);
    internal async Task<ClaudeNativeSend.Result> SendAsync(string text,Func<ClaudeHistory.Value> history,Action dispatch,Func<bool> authorized,CancellationToken token){
        if(!authorized())return new(false,false,null,"unauthorized");
        if(string.IsNullOrWhiteSpace(text)||new UTF8Encoding(false,true).GetByteCount(text)>2000||text.Any(char.IsControl))return new(false,false,null,"native_input_unsupported");
        var original=Read();if(!original.Empty)return new(false,false,null,"desktop_draft_present");
        if(!await RestoreForInputAsync(original.Window,authorized,token))return new(false,false,null,"native_window_restore_unconfirmed");
        if(Held.Any(k=>(GetAsyncKeyState(k)&0x8000)!=0))return new(false,false,null,"desktop_input_active");
        // Only an explicit authorized device request reaches this activation.
        if(!authorized()||!Tab5DesktopActivation.Raise(original.Window).Foreground)return new(false,false,null,"native_foreground_unconfirmed");
        original.Editor.SetFocus();
        string target=string.Join(",",original.Frame.GetRuntimeId());
        var calls=new ClaudeNativeSend.Calls(
            ()=>{var view=Read();return new(_session,Scope(),string.Join(",",view.Frame.GetRuntimeId()),view.Text,view.Empty,true,view.Button.Current.IsEnabled);},Scope,
            input=>{var view=Read();Require(view.Empty&&Focused(view)&&string.Join(",",view.Frame.GetRuntimeId())==target&&!Held.Any(k=>(GetAsyncKeyState(k)&0x8000)!=0),"native_composer_changed");
                var inputs=new List<Input>();foreach(char c in input){inputs.Add(Key(c,false));inputs.Add(Key(c,true));}
                Require(SendInput((uint)inputs.Count,inputs.ToArray(),Marshal.SizeOf<Input>())==inputs.Count,"native_input_unconfirmed");},
            ()=>{var view=Read();Require(authorized()&&Focused(view)&&view.Text==text&&view.Button.Current.IsEnabled&&string.Join(",",view.Frame.GetRuntimeId())==target,"native_composer_changed");((InvokePattern)view.Button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();},
            history,authorized,dispatch,ct=>Task.Delay(100,ct));
        return await ClaudeNativeSend.SendAsync(calls,text,token);
    }
    private static readonly int[] Held=[0x10,0x11,0x12,0x5B,0x5C,0x01,0x02];
    private static string ReadDatabase(string path,string key){
        Require((File.GetAttributes(path)&FileAttributes.ReparsePoint)==0,"native_state_unconfirmed");nint db=0,stmt=0;
        try{Require(sqlite3_open_v2(path,out db,1,0)==0,"native_state_unconfirmed");sqlite3_busy_timeout(db,100);
            Require(key is "Anthropic.claude-code" or "memento/workbench.parts.editor","native_state_unconfirmed");
            Require(sqlite3_prepare_v2(db,"SELECT value FROM ItemTable WHERE key='"+key+"'",-1,out stmt,0)==0&&sqlite3_step(stmt)==100,"native_state_unconfirmed");
            int length=sqlite3_column_bytes(stmt,0);Require(length is >0 and <=262144,"native_state_unconfirmed");return Marshal.PtrToStringUTF8(sqlite3_column_text(stmt,0),length)??throw new IOException("native_state_unconfirmed");
        }finally{if(stmt!=0)sqlite3_finalize(stmt);if(db!=0)sqlite3_close(db);}
    }
    [StructLayout(LayoutKind.Sequential)]private struct BasicInfo{public nint Reserved1,Peb,Reserved2A,Reserved2B,Pid,Parent;}
    [StructLayout(LayoutKind.Sequential)]private struct Input{public uint Type;public InputUnion Data;}
    [StructLayout(LayoutKind.Explicit)]private struct InputUnion{[FieldOffset(0)]public KeyboardInput Keyboard;[FieldOffset(0)]public MouseInput Mouse;}
    [StructLayout(LayoutKind.Sequential)]private struct KeyboardInput{public ushort Vk,Scan;public uint Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)]private struct MouseInput{public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
    private static Input Key(char c,bool up)=>new(){Type=1,Data=new InputUnion{Keyboard=new KeyboardInput{Scan=c,Flags=4u|(up?2u:0u)}}};
    private delegate bool EnumCallback(nint window,nint parameter);
    [DllImport("user32.dll")]private static extern bool EnumWindows(EnumCallback callback,nint parameter);
    [DllImport("user32.dll")]private static extern bool IsWindowVisible(nint window);
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint window,out uint pid);
    [DllImport("user32.dll")]private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint count,Input[] inputs,int size);
    [DllImport("ntdll.dll")]private static extern int NtQueryInformationProcess(nint process,int infoClass,out BasicInfo info,int length,out int returned);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sqlite3_open_v2([MarshalAs(UnmanagedType.LPUTF8Str)]string path,out nint db,int flags,nint vfs);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sqlite3_busy_timeout(nint db,int ms);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sqlite3_prepare_v2(nint db,[MarshalAs(UnmanagedType.LPUTF8Str)]string sql,int length,out nint stmt,nint tail);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sqlite3_step(nint stmt);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]private static extern nint sqlite3_column_text(nint stmt,int column);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sqlite3_column_bytes(nint stmt,int column);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sqlite3_finalize(nint stmt);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)]private static extern int sqlite3_close(nint db);
}
