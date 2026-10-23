using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AIBotBridge;

// Positive cover detection only. Access denied / another window never means
// that a PIN field, credential provider or keyboard focus has been verified.
internal sealed record ComputerLockScreenState(int Version=1,bool? Locked=null,bool? LockScreenCover=null,int? DisplayState=null);
internal static class WindowsLockScreen
{
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr window,StringBuilder name,int count);
    internal static bool IsCover(bool? locked,string windowClass,string? image,int session,int currentSession,string windows)=>
        locked==true&&session==currentSession&&windowClass=="Windows.UI.Core.CoreWindow"&&
        string.Equals(image,Path.Combine(windows,"SystemApps","Microsoft.LockApp_cw5n1h2txyewy","LockApp.exe"),StringComparison.OrdinalIgnoreCase);
    private static ComputerLockScreenState Snapshot(bool? locked,bool? cover=null)=>
        new(Locked:locked,LockScreenCover:cover,DisplayState:WindowsDisplayPower.Read());
    internal static ComputerLockScreenState Read()
    {
        bool? locked=WindowsSessionLock.Read();
        if(locked!=true)return Snapshot(locked);
        try {
            IntPtr foreground=GetForegroundWindow();
            if(foreground==IntPtr.Zero)return Snapshot(locked);
            GetWindowThreadProcessId(foreground,out uint pid);
            var windowClass=new StringBuilder(256);GetClassName(foreground,windowClass,windowClass.Capacity);
            using var process=Process.GetProcessById(checked((int)pid));
            using var current=Process.GetCurrentProcess();
            bool cover=IsCover(locked,windowClass.ToString(),process.MainModule?.FileName,process.SessionId,current.SessionId,
                Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            // Reject a transition while collecting metadata. Never switch or
            // open the secure desktop and never inspect text / control values.
            bool? latest=WindowsSessionLock.Read();
            return Snapshot(latest,cover&&latest==true&&foreground==GetForegroundWindow()?true:null);
        }catch(Exception ex) when(ex is System.ComponentModel.Win32Exception or InvalidOperationException or ArgumentException or NotSupportedException or OverflowException or UnauthorizedAccessException) {
            return Snapshot(WindowsSessionLock.Read());
        }
    }
}
