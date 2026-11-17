using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AIBotBridge;

internal static class ForegroundObserver
{
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window,out uint id);
    internal static bool CodexVisible()
    {
        try
        {
            GetWindowThreadProcessId(GetForegroundWindow(),out var id);
            if(id==0)return false;
            using var process=Process.GetProcessById((int)id);
            return process.ProcessName.Equals("codex",StringComparison.OrdinalIgnoreCase) ||
                process.ProcessName.Equals("ChatGPT",StringComparison.OrdinalIgnoreCase)&&
                (process.MainModule?.FileName.Contains("OpenAI.Codex_",StringComparison.OrdinalIgnoreCase)??false);
        }
        catch(Exception ex) when(ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception) { return false; }
    }
}
