using System.Runtime.InteropServices;
using System.Diagnostics;

namespace AIBotBridge;

// Only an explicit device click may use one guarded Alt tap. Never send Enter,
// text, an IME shortcut, or change the system foreground-lock configuration.
internal static class Tab5DesktopActivation
{
    internal sealed record Calls(Func<nint,bool> Activate,Func<nint> Foreground,
        Func<bool> InputHeld,Func<uint> TapAlt,Action ReleaseAlt,Func<bool>? Interactive=null);
    internal sealed record ThreadLaunchCalls(Func<bool> Interactive,Func<bool> Elevated,
        Func<string,bool> OpenInExplorer,Func<string,bool> OpenDirect);
    internal readonly record struct Result(bool Foreground,bool Tapped,bool InputBlocked,bool PartialTap=false);
    internal enum InputAvailability { Ready, ForegroundPending, DesktopUnavailable, InputActive }
    private static readonly int[] HeldKeys=[0x10,0x11,0x12,0x5B,0x5C,0x01,0x02];
    private static readonly Calls Native=new(SetForegroundWindow,GetForegroundWindow,
        ()=>HeldKeys.Any(key=>(GetAsyncKeyState(key)&0x8000)!=0),
        ()=>SendInput(2,[Key(false),Key(true)],Marshal.SizeOf<Input>()),
        ()=>SendInput(1,[Key(true)],Marshal.SizeOf<Input>()),IsInteractive);
    private static readonly ThreadLaunchCalls ThreadLauncher=new(IsInteractive,()=>BridgeElevation.IsAdministrator,OpenInExplorer,OpenDirect);
    internal static bool OpenCodexThread(string id,ThreadLaunchCalls? calls=null) {
        calls??=ThreadLauncher;
        if(!Guid.TryParseExact(id,"D",out _)||!calls.Interactive())return false;
        string uri=Tab5CodexDesktop.ThreadUri(id);
        return (calls.Elevated()?calls.OpenInExplorer:calls.OpenDirect)(uri);
    }
    private static bool OpenDirect(string uri) {
        try {
            using var process=Process.Start(new ProcessStartInfo(uri){UseShellExecute=true});
            return true;
        }catch(Exception ex) when(ex is InvalidOperationException or System.ComponentModel.Win32Exception){return false;}
    }
    private static bool OpenInExplorer(string uri) {
        try {
            var shellType=Type.GetTypeFromProgID("Shell.Application");
            if(shellType is null||Activator.CreateInstance(shellType) is not {} shell)return false;
            dynamic windows=((dynamic)shell).Windows();
            object location=0,root=0;
            int desktopHandle;
            dynamic? desktop=windows.FindWindowSW(ref location,ref root,8,out desktopHandle,1);
            if(desktop is null||desktopHandle==0)return false;
            GetWindowThreadProcessId(GetShellWindow(),out uint shellPid);
            GetWindowThreadProcessId((nint)desktopHandle,out uint desktopPid);
            if(shellPid==0||desktopPid!=shellPid||!IsInteractive())return false;
            // The existing desktop's shell resolves the registered handler in
            // the interactive user's context, including packaged Codex.
            desktop.Document.Application.ShellExecute(uri,"","","open",1);
            return true;
        }catch(Exception ex) when(ex is COMException or InvalidOperationException or System.ComponentModel.Win32Exception or
            Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){return false;}
    }
    // UOI_IO checks whether our desktop currently receives input. Lock screens,
    // secure desktops and disconnected sessions must not trigger activation.
    private static bool InputDesktopActive()=>
        GetUserObjectInformation(GetThreadDesktop(GetCurrentThreadId()),6,out int active,sizeof(int),out _)&&active!=0;
    internal static bool IsInteractive()=>InputDesktopActive()&&ForegroundAllowed(GetForegroundWindow());
    internal static InputAvailability ReadInputAvailability(){
        if(!InputDesktopActive())return InputAvailability.DesktopUnavailable;
        if(Native.InputHeld())return InputAvailability.InputActive;
        nint foreground=GetForegroundWindow();
        return foreground==0?InputAvailability.ForegroundPending:
            ForegroundAllowed(foreground)?InputAvailability.Ready:InputAvailability.DesktopUnavailable;
    }
    private static bool ForegroundAllowed(nint window){
        if(window==0)return false;
        GetWindowThreadProcessId(window,out uint pid);
        if(pid==0)return false;
        try{
            using var foreground=Process.GetProcessById((int)pid);
            return ForegroundAllowed(foreground.ProcessName);
        }catch(Exception e) when(e is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception){return false;}
    }
    internal static bool ForegroundAllowed(string name)=>
        !name.Equals("LockApp",StringComparison.OrdinalIgnoreCase)&&
        !name.Equals("LogonUI",StringComparison.OrdinalIgnoreCase)&&
        !name.Equals("CredentialUIBroker",StringComparison.OrdinalIgnoreCase);
    internal static Result Raise(nint window,bool allowInput=true,Calls? calls=null) {
        calls??=Native;
        if(window==0)return new(false,false,false);
        if(calls.Interactive?.Invoke()==false)return new(false,false,true);
        if(calls.Foreground()==window)return new(true,false,false);
        calls.Activate(window);
        if(calls.Foreground()==window)return new(true,false,false);
        if(!allowInput)return new(false,false,false);
        if(calls.Foreground()==0||calls.InputHeld()||calls.Interactive?.Invoke()==false)return new(false,false,true);
        uint sent=calls.TapAlt();
        if(sent==1)calls.ReleaseAlt();
        if(sent!=2)return new(false,sent!=0,false,sent==1);
        calls.Activate(window);
        return new(calls.Foreground()==window,true,false);
    }
    private static Input Key(bool up)=>new(){Type=1,Data=new InputUnion{Keyboard=new KeyboardInput{Vk=0xA4,Flags=up?2u:0u}}};
    [StructLayout(LayoutKind.Sequential)]private struct Input {public uint Type;public InputUnion Data;}
    [StructLayout(LayoutKind.Explicit)]private struct InputUnion {[FieldOffset(0)]public KeyboardInput Keyboard;[FieldOffset(0)]public MouseInput Mouse;}
    [StructLayout(LayoutKind.Sequential)]private struct KeyboardInput {public ushort Vk,Scan;public uint Flags,Time;public UIntPtr Extra;}
    [StructLayout(LayoutKind.Sequential)]private struct MouseInput {public int X,Y;public uint Data,Flags,Time;public UIntPtr Extra;}
    [DllImport("user32.dll")]private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")]private static extern nint GetShellWindow();
    [DllImport("user32.dll")]private static extern uint GetWindowThreadProcessId(nint window,out uint pid);
    [DllImport("kernel32.dll")]private static extern uint GetCurrentThreadId();
    [DllImport("user32.dll")]private static extern nint GetThreadDesktop(uint threadId);
    [DllImport("user32.dll",EntryPoint="GetUserObjectInformationW")]private static extern bool GetUserObjectInformation(nint desktop,int index,out int value,int length,out int needed);
    [DllImport("user32.dll")]private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint count,Input[] inputs,int size);
}
