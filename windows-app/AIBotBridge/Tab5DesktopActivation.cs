using System.Runtime.InteropServices;

namespace AIBotBridge;

// Only an explicit device click may use one guarded Alt tap. Never send Enter,
// text, an IME shortcut, or change the system foreground-lock configuration.
internal static class Tab5DesktopActivation
{
    internal sealed record Calls(Func<nint,bool> Activate,Func<nint> Foreground,
        Func<bool> InputHeld,Func<uint> TapAlt,Action ReleaseAlt);
    internal readonly record struct Result(bool Foreground,bool Tapped,bool InputBlocked,bool PartialTap=false);
    private static readonly int[] HeldKeys=[0x10,0x11,0x12,0x5B,0x5C,0x01,0x02];
    private static readonly Calls Native=new(SetForegroundWindow,GetForegroundWindow,
        ()=>HeldKeys.Any(key=>(GetAsyncKeyState(key)&0x8000)!=0),
        ()=>SendInput(2,[Key(false),Key(true)],Marshal.SizeOf<Input>()),
        ()=>SendInput(1,[Key(true)],Marshal.SizeOf<Input>()));
    internal static Result Raise(nint window,bool allowInput=true,Calls? calls=null) {
        calls??=Native;
        if(window==0)return new(false,false,false);
        if(calls.Foreground()==window)return new(true,false,false);
        calls.Activate(window);
        if(calls.Foreground()==window)return new(true,false,false);
        if(!allowInput)return new(false,false,false);
        if(calls.Foreground()==0||calls.InputHeld())return new(false,false,true);
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
    [DllImport("user32.dll")]private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")]private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll",SetLastError=true)]private static extern uint SendInput(uint count,Input[] inputs,int size);
}
