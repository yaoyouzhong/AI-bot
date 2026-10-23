using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace AIBotBridge;

// Local interoperability with the installed Doubao service. No vendor binary
// is distributed or modified. Validate actual replies, not a version allowlist.
// Keep explicit start/stop separate: a repeated toggle can start recording.
internal sealed class Tab5DoubaoVoice(Func<int,int> send,Func<bool> safeFocus)
{
    internal const int Query=1006,StartCapture=1007,StopCapture=1008,ShowCapture=1012;
    private bool _ownsCapture;
    private bool _stopRequested;
    internal bool IsIdle=>send(Query)==0;
    internal bool CanRestoreFocus {
        get {
            if(!_ownsCapture||_stopRequested)return false;
            int state=send(Query);
            return state>=0&&(state&1)!=0;
        }
    }
    internal bool CaptureStopped {
        get {
            if(!_stopRequested&&!_ownsCapture)return true;
            // The IME can also stop itself after a focus change. Confirm that
            // case without sending another control to a different input owner.
            int state=send(Query);
            if(state!=0)return false;
            _ownsCapture=false;_stopRequested=false;return true;
        }
    }
    internal bool Start() {
        if(!safeFocus())return false;
        int before=send(Query);
        if(before!=0)throw new InvalidOperationException(before<0?"无法连接豆包语音服务":"豆包正在处理其他语音，请先结束后重试");
        _stopRequested=false;
        if(!safeFocus())return false;
        int result=send(StartCapture);
        int after=send(Query);
        Tab5VoiceTiming.Log($"doubao-rpc-start result={result} state={after}",Environment.TickCount64);
        _ownsCapture=result>=0&&after>=0&&(after&1)!=0;
        if(result>=0&&after<0&&safeFocus())send(StopCapture); // explicit cleanup, never toggle/retry
        if(!_ownsCapture)throw new InvalidOperationException("豆包未确认开始录音，请检查网络及输入法状态");
        // PRESS_START only pre-arms the recorder. SHOW_WAVE materializes the
        // voice session and notifies TSF; otherwise PRESS_STOP discards it.
        if(!safeFocus())throw new InvalidOperationException("语音输入焦点已变化，请先结束电脑上的录音");
        int shown=send(ShowCapture);
        Tab5VoiceTiming.Log($"doubao-rpc-show result={shown}",Environment.TickCount64);
        if(shown!=0) {
            send(StopCapture);_stopRequested=true;
            throw new InvalidOperationException("豆包识别窗口未就绪，请结束后重试");
        }
        return true;
    }
    internal bool Stop() {
        if(!_ownsCapture||_stopRequested)return false;
        // Do not send controls after the user moves to a different input owner.
        if(!safeFocus())return false;
        int before=send(Query);
        if(before<0)return false;
        if((before&1)==0){_ownsCapture=false;_stopRequested=before!=0;return true;}
        int result=send(StopCapture),after=send(Query);
        Tab5VoiceTiming.Log($"doubao-rpc-stop result={result} state={after}",Environment.TickCount64);
        if(result<0||after<0)return false;
        // The service acknowledges before its recording worker has finished.
        // Keep polling through the UI timer; never resend/toggle or block TSF.
        _stopRequested=true;
        return true;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int SimpleMessage([MarshalAs(UnmanagedType.LPUTF8Str)]string address,int message,int wparam,int lparam);
    private static SimpleMessage? _native;
    private static readonly object Gate=new();
    private const string Pipe=@"\\.\pipe\ObricIme\oime-server";
    internal static Tab5DoubaoVoice? TryCreate(Func<bool> safeFocus) {
        lock(Gate) {
            if(_native is null) {
                try {
                    // Probe the running service before taking focus.
                    // A focused TSF control is still required for actual capture.
                    string? service=null;
                    foreach(var process in Process.GetProcessesByName("ImeService")) {
                        using(process) {
                            try {service=ProcessImage(process.Id);}
                            catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException) {continue;}
                        }
                        if(service is not null)break;
                    }
                    if(service is null)return null;
                    string directory=Path.GetDirectoryName(service)!;
                    // Load dependencies only beside the installed service and in normal
                    // system locations, never from the current working directory.
                    IntPtr module=LoadLibraryEx(Path.Combine(directory,"rpc.dll"),IntPtr.Zero,0x1100);
                    if(module==IntPtr.Zero)return null;
                    if(!NativeLibrary.TryGetExport(module,"RpcPipe_SimpleMessage",out var address)){NativeLibrary.Free(module);return null;}
                    _native=Marshal.GetDelegateForFunctionPointer<SimpleMessage>(address);
                    // Deliberately retain the module for the delegate's process lifetime.
                }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or ArgumentException){return null;}
            }
            var call=_native;
            return new(message=>call(Pipe,message,0,0),safeFocus);
        }
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
    private static string? ProcessImage(int id) {
        // UIAccess/elevated IME workers need not grant PROCESS_VM_READ, which
        // Process.MainModule requires. Query only their executable path.
        using var process=OpenProcess(0x1000,false,id);
        if(process.IsInvalid)return null;
        var path=new StringBuilder(32768);int length=path.Capacity;
        return QueryFullProcessImageName(process,0,path,ref length)?path.ToString():null;
    }
    [DllImport("kernel32.dll",SetLastError=true)]
    private static extern SafeProcessHandle OpenProcess(uint access,[MarshalAs(UnmanagedType.Bool)]bool inherit,int id);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(SafeProcessHandle process,int flags,StringBuilder name,ref int length);
}
