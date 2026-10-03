using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace AIBotBridge;

// Local interoperability with verified installed Doubao builds. No vendor
// binary is distributed or modified. Unknown builds fail closed to USB.
// Keep explicit start/stop separate: a repeated toggle can start recording.
internal sealed class Tab5DoubaoVoice(Func<int,int> send,Func<bool> safeFocus)
{
    internal const int Query=1006,StartCapture=1007,StopCapture=1008,ShowCapture=1012;
    private bool _ownsCapture;
    private bool _stopRequested;
    internal bool IsIdle=>send(Query)==0;
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
    private static readonly (string Name,string Sha)[][] Builds=[[
        ("rpc.dll","A3EAD1A55850257BAC01A878C899F42291A1F41BD2B834E0CAA5C5B66A674E02"),
        ("tsf-oime-core.dll","77D58BFC5BBC9016EE58135967603FBAB19A30E79A1C338BCE6A2DF62CE60A83"),
        ("ImeService.exe","94B17BDCA571AC3CD2DAFA687B4CB8E3A789CDA9D044276483003B0A9E8F77A6")],
        // 0.9.1.22: same explicit controls and query bitset, including pending
        // stop/commit flags. Keep the entire build matched, never a DLL alone.
        [("rpc.dll","0BE0CB35D864D06B2C8B5267D9F0669A1383493F557A45C6A1EBBFA203E85E53"),
        ("tsf-oime-core.dll","8544BFB87D8D2CC847B13E2CCC9BBD2220B20BCEAFDD1FC88AD5EAFF5A28BB02"),
        ("ImeService.exe","94ACE7E504E6AA70C15095D5219604AEE93E17247EB85429A046C7A4FDB95E90")]];
    internal static bool MatchesBuild(Func<string,string?> hash)=>Builds.Any(build=>build.All(file=>
        string.Equals(hash(file.Name),file.Sha,StringComparison.OrdinalIgnoreCase)));
    internal static Tab5DoubaoVoice? TryCreate(Func<bool> safeFocus) {
        lock(Gate) {
            if(_native is null) {
                try {
                    // Use the TSF module already loaded into our own draft process.
                    string? tsf=Process.GetCurrentProcess().Modules.Cast<ProcessModule>()
                        .FirstOrDefault(m=>m.ModuleName.Equals("tsf-oime-core.dll",StringComparison.OrdinalIgnoreCase))?.FileName;
                    if(tsf is null)return null;
                    string directory=Path.GetDirectoryName(tsf)!;
                    var hashes=new Dictionary<string,string?>();
                    foreach(var name in Builds.SelectMany(build=>build).Select(file=>file.Name).Distinct()) {
                        string file=Path.Combine(directory,name);
                        hashes[name]=File.Exists(file)?Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))):null;
                    }
                    if(!MatchesBuild(name=>hashes[name]))return null;
                    // Load dependencies only beside the verified DLL and in normal
                    // system locations, never from the current working directory.
                    IntPtr module=LoadLibraryEx(Path.Combine(directory,"rpc.dll"),IntPtr.Zero,0x1100);
                    if(module==IntPtr.Zero)return null;
                    _native=Marshal.GetDelegateForFunctionPointer<SimpleMessage>(NativeLibrary.GetExport(module,"RpcPipe_SimpleMessage"));
                    // Deliberately retain the module for the delegate's process lifetime.
                }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or ArgumentException){return null;}
            }
            var call=_native;
            return new(message=>call(Pipe,message,0,0),safeFocus);
        }
    }
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
    private static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
}
