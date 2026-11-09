using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace AIBotBridge;

// Observes preparation only. It never answers a permission prompt or activates
// another application. A user switch to an unrelated editor revokes recovery.
internal sealed class Tab5VoiceStartupFocus : IDisposable
{
    internal enum Owner { Draft, Previous, Permission, PendingPermission, Other, Unavailable }
    internal sealed class Policy
    {
        private bool _permission;
        private bool _pendingPicker;
        private int _pendingSamples;
        private int _unavailableSamples;
        internal bool Revoked {get;private set;}
        internal bool Waiting {get;private set;}
        internal bool SawPermission {get;private set;}
        internal void Observe(Owner owner,bool inputHeld)
        {
            if(Revoked)return;
            if(owner==Owner.Unavailable){
                // The system briefly reports no foreground during transition
                // out of consent. Never activate while unavailable, and only
                // wait if a verified permission or its dimmer was observed.
                if((_permission||_pendingPicker)&&!(_pendingPicker&&inputHeld)&&++_unavailableSamples<=80){Waiting=true;return;}
                Revoked=true;Waiting=false;return;
            }
            _unavailableSamples=0;
            if(owner==Owner.PendingPermission){
                // PickerHost first raises an untitled dimming window. It is not
                // proof of consent: wait briefly without activation until the
                // owned microphone dialog appears, otherwise fail closed.
                _pendingPicker=true;Waiting=true;
                if(inputHeld||++_pendingSamples>80){Revoked=true;Waiting=false;}
                return;
            }
            if(owner==Owner.Permission){SawPermission=true;Waiting=true;_permission=true;_pendingPicker=false;_pendingSamples=0;return;}
            if(_pendingPicker){Revoked=true;Waiting=false;return;}
            // Let the approval click finish before activating our editor. Pointer
            // movement alone is not typing and must not revoke an explicit take.
            bool returned=_permission&&owner is Owner.Draft or Owner.Previous;
            if(returned&&inputHeld){Waiting=true;return;}
            Waiting=false;
            if(owner==Owner.Other||inputHeld)Revoked=true;
            _permission=false;
        }
    }
    private readonly nint _draft,_previous;
    private readonly object _gate=new();
    private readonly Policy _policy=new();
    private readonly System.Threading.Timer _observer;
    private readonly Dictionary<uint,bool> _permissionProcesses=[];
    private readonly Func<nint,bool>? _permissionWindow;
    private bool _disposed;
    internal bool Waiting {get {lock(_gate)return _policy.Waiting;}}
    internal bool Revoked {get {lock(_gate)return _policy.Revoked;}}
    internal bool SawPermission {get {lock(_gate)return _policy.SawPermission;}}
    internal void Activate(Action activation){
        lock(_gate){
            Sample();
            if(_policy.Revoked||_policy.Waiting)return;
            activation();
        }
    }
    internal Tab5VoiceStartupFocus(nint draft,nint previous,Func<nint,bool>? permissionWindow=null)
    {
        _draft=draft;_previous=previous;_permissionWindow=permissionWindow;Sample();
        // Wasapi initialization may block the STA. Keep observing that interval.
        _observer=new(_=>Sample(),null,25,25);
    }
    private void Sample()
    {
        lock(_gate){
            if(_disposed)return;
            var foreground=GetForegroundWindow();
            Owner owner=foreground==_draft?Owner.Draft:foreground==_previous?Owner.Previous:
                foreground==0?Owner.Unavailable:IsPermission(foreground)?Owner.Permission:
                    IsPendingPicker(foreground)?Owner.PendingPermission:Owner.Other;
            // Only the pressed/not-pressed state is observed. No key codes or
            // characters are retained. GetLastInputInfo includes harmless motion.
            bool inputHeld=Enumerable.Range(1,254).Any(key=>(GetAsyncKeyState(key)&0x8000)!=0);
            _policy.Observe(owner,inputHeld);
        }
    }
    private static bool IsPendingPicker(nint window)
    {
        GetWindowThreadProcessId(window,out uint pid);if(pid==0)return false;
        using var process=OpenProcess(0x1000,false,pid);if(process.IsInvalid)return false;
        var path=new StringBuilder(1024);int size=path.Capacity;if(!QueryFullProcessImageName(process,0,path,ref size))return false;
        string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);var windowClass=new StringBuilder(256);var title=new StringBuilder(512);
        return !string.IsNullOrEmpty(windows)&&string.Equals(path.ToString(),Path.Combine(windows,"System32","PickerHost.exe"),StringComparison.OrdinalIgnoreCase)&&
            GetClassName(window,windowClass,windowClass.Capacity)>0&&windowClass.ToString()=="Shell_SystemDim"&&GetWindowText(window,title,title.Capacity)==0;
    }
    private bool IsPermission(nint window)
    {
        if(_permissionWindow is not null)return _permissionWindow(window);
        GetWindowThreadProcessId(window,out uint pid);if(pid==0)return false;
        if(_permissionProcesses.TryGetValue(pid,out bool known))return known||OwnedMicrophonePicker(window,pid);
        if(_permissionProcesses.Count>=32)return false;
        string path="";
        using(var process=OpenProcess(0x1000,false,pid)){
            if(!process.IsInvalid){var value=new StringBuilder(1024);int size=value.Capacity;if(QueryFullProcessImageName(process,0,value,ref size))path=value.ToString();}
        }
        bool permission=PermissionProcess(path,Environment.GetFolderPath(Environment.SpecialFolder.Windows));
        _permissionProcesses[pid]=permission;return permission||OwnedMicrophonePicker(window,pid,path);
    }
    private static bool OwnedMicrophonePicker(nint window,uint pid,string? path=null)
    {
        if(path is null){using var process=OpenProcess(0x1000,false,pid);if(process.IsInvalid)return false;
            var value=new StringBuilder(1024);int size=value.Capacity;if(!QueryFullProcessImageName(process,0,value,ref size))return false;path=value.ToString();}
        string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if(string.IsNullOrEmpty(windows)||!string.Equals(path,Path.Combine(windows,"System32","PickerHost.exe"),StringComparison.OrdinalIgnoreCase))return false;
        var name=new StringBuilder(256);var title=new StringBuilder(512);
        if(GetClassName(window,name,name.Capacity)==0||GetWindowText(window,title,title.Capacity)==0)return false;
        string executable=Environment.ProcessPath??"";var names=new List<string>();
        if(executable.Length>0){names.Add(Path.GetFileNameWithoutExtension(executable));
            try{var info=FileVersionInfo.GetVersionInfo(executable);names.Add(info.FileDescription??"");names.Add(info.ProductName??"");}
            catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception){}}
        return OwnedMicrophonePicker(path,windows,name.ToString(),title.ToString(),names);
    }
    internal static bool OwnedMicrophonePicker(string path,string windows,string windowClass,string title,IEnumerable<string> names)
    {
        if(string.IsNullOrEmpty(windows)||!string.Equals(path,Path.Combine(windows,"System32","PickerHost.exe"),StringComparison.OrdinalIgnoreCase)||
            windowClass!="Shell_SystemDialog")return false;
        foreach(string name in names.Where(n=>!string.IsNullOrWhiteSpace(n)).Distinct(StringComparer.OrdinalIgnoreCase)){
            string expected=$"允许 {name} 访问你的麦克风";
            if(string.Equals(title,expected+"?",StringComparison.OrdinalIgnoreCase)||
                string.Equals(title,expected+"？",StringComparison.OrdinalIgnoreCase)||
                string.Equals(title,$"Allow {name} to access your microphone?",StringComparison.OrdinalIgnoreCase))return true;
        }
        return false;
    }
    internal static bool PermissionProcess(string path,string windows)
    {
        // These are protected Windows permission hosts, not a title/name match
        // against arbitrary user programs. No elevation or global permission edit.
        if(string.IsNullOrEmpty(path)||string.IsNullOrEmpty(windows))return false;
        string system=Path.Combine(windows,"System32");
        if(new[]{"RuntimeBroker.exe","consent.exe","ConsentUXClient.exe"}.Any(name=>
            string.Equals(path,Path.Combine(system,name),StringComparison.OrdinalIgnoreCase)))return true;
        return string.Equals(path,Path.Combine(windows,"SystemApps","Microsoft.Windows.AppRep.ChxApp_cw5n1h2txyewy","CHXSmartScreen.exe"),StringComparison.OrdinalIgnoreCase);
    }
    internal async Task<bool> RestoreAsync(Func<bool> focused,Action restore,CancellationToken token,Action<bool>? waiting=null)
    {
        long started=Environment.TickCount64,stable=-1,next=0;
        while(Environment.TickCount64-started<60000){
            token.ThrowIfCancellationRequested();Sample();
            if(Revoked)return false;
            bool prompt=Waiting;waiting?.Invoke(prompt);
            long now=Environment.TickCount64;
            if(prompt){stable=-1;}
            else if(focused()){
                if(stable<0)stable=now;
                if(now-stable>=350)return true;
            }else{
                stable=-1;
                if(now>=next){restore();next=now+100;}
                // A known prompt may take time. Ordinary focus acquisition stays bounded.
                if(!SawPermission&&now-started>=2200)return false;
            }
            await Task.Delay(50,token);
        }
        return false;
    }
    public void Dispose(){lock(_gate){_disposed=true;}_observer.Dispose();}
    [DllImport("user32.dll")]private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint window,out uint process);
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetClassName(nint window,StringBuilder value,int size);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern int GetWindowText(nint window,StringBuilder value,int size);
    [DllImport("kernel32.dll",SetLastError=true)]private static extern SafeProcessHandle OpenProcess(uint access,bool inherit,uint pid);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]private static extern bool QueryFullProcessImageName(SafeProcessHandle process,int flags,StringBuilder path,ref int size);
}
