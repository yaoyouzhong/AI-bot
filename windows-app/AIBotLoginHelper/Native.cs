using System.Runtime.InteropServices;
using System.Text;
internal static class Native
{
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct StartupInfo {
        public int Size; public string? Reserved, Desktop, Title;
        public int X, Y, XSize, YSize, XChars, YChars, Fill, Flags;
        public short Show, ReservedSize; public nint ReservedPtr, StdInput, StdOutput, StdError;
    }
    [StructLayout(LayoutKind.Sequential)] internal struct ProcessInfo { public nint Process, Thread; public uint ProcessId, ThreadId; }
    [StructLayout(LayoutKind.Sequential)] internal struct Luid { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)] internal struct Privileges { public uint Count; public Luid Luid; public uint Attributes; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Level1 {
        public uint SessionId; public int State, Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 33)] public string Station;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 21)] public string User;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 18)] public string Domain;
        public long Logon, Connect, Disconnect, LastInput, Current;
        public uint IncomingBytes, OutgoingBytes, IncomingFrames, OutgoingFrames, IncomingCompressed, OutgoingCompressed;
    }
    [StructLayout(LayoutKind.Sequential)] private struct InfoEx { public uint Level; public Level1 Data; }
    internal static bool? Locked(int session) {
        if (!WTSQuerySessionInformation(0, session, 25, out nint buffer, out int size)) return null;
        try {
            if (size < Marshal.SizeOf<InfoEx>()) return null;
            var value = Marshal.PtrToStructure<InfoEx>(buffer);
            if (value.Level != 1 || value.Data.SessionId != session) return null;
            return value.Data.Flags switch { 0 => true, 1 => false, _ => null };
        } finally { WTSFreeMemory(buffer); }
    }
    internal static string DesktopName(nint desktop) {
        var value = new StringBuilder(128);
        return GetUserObjectInformation(desktop, 2, value, value.Capacity * 2, out _) ? value.ToString() : "unknown";
    }
    internal static void Check(bool ok) { if (!ok) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error()); }
    internal static void EnableTcb(nint token) {
        Check(LookupPrivilegeValue(null, "SeTcbPrivilege", out var luid));
        var privileges = new Privileges { Count = 1, Luid = luid, Attributes = 2 };
        Check(AdjustTokenPrivileges(token, false, ref privileges, 0, 0, 0));
        int error = Marshal.GetLastWin32Error();
        if (error != 0) throw new System.ComponentModel.Win32Exception(error);
    }
    internal delegate bool EnumWindows(nint hwnd, nint arg);
    [DllImport("kernel32.dll")] internal static extern nint GetCurrentProcess();
    [DllImport("kernel32.dll")] internal static extern uint GetCurrentThreadId();
    [DllImport("kernel32.dll")] internal static extern uint WTSGetActiveConsoleSessionId();
    [DllImport("wtsapi32.dll", SetLastError = true)] internal static extern bool WTSQueryUserToken(uint session, out nint token);
    [DllImport("kernel32.dll")] internal static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll")] internal static extern uint WaitForSingleObject(nint handle, uint ms);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool GetExitCodeProcess(nint process, out uint code);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool TerminateProcess(nint process, uint code);
    [DllImport("user32.dll")] internal static extern nint GetThreadDesktop(uint thread);
    [DllImport("user32.dll", SetLastError = true)] internal static extern nint OpenInputDesktop(uint flags, bool inherit, uint access);
    [DllImport("user32.dll")] internal static extern bool CloseDesktop(nint desktop);
    [DllImport("user32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetUserObjectInformationW", SetLastError = true)] private static extern bool GetUserObjectInformation(nint handle, int info, StringBuilder value, int size, out int needed);
    [DllImport("user32.dll")] internal static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(nint hwnd, out uint process);
    [DllImport("user32.dll")] internal static extern bool IsWindowVisible(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)] internal static extern bool EnumDesktopWindows(nint desktop, EnumWindows callback, nint arg);
    [DllImport("wtsapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "WTSQuerySessionInformationW", SetLastError = true)] private static extern bool WTSQuerySessionInformation(nint server, int session, int info, out nint buffer, out int size);
    [DllImport("wtsapi32.dll")] private static extern void WTSFreeMemory(nint buffer);
    [DllImport("advapi32.dll", SetLastError = true)] internal static extern bool OpenProcessToken(nint process, uint access, out nint token);
    [DllImport("advapi32.dll", SetLastError = true)] internal static extern bool DuplicateTokenEx(nint token, uint access, nint security, int impersonation, int type, out nint duplicate);
    [DllImport("advapi32.dll", SetLastError = true)] internal static extern bool SetTokenInformation(nint token, int info, ref int value, int size);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] private static extern bool LookupPrivilegeValue(string? system, string name, out Luid value);
    [DllImport("advapi32.dll", SetLastError = true)] private static extern bool AdjustTokenPrivileges(nint token, bool disableAll, ref Privileges privileges, int size, nint previous, nint needed);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, EntryPoint = "CreateProcessAsUserW", SetLastError = true)] internal static extern bool CreateProcessAsUser(nint token, string application, StringBuilder command, nint processSecurity, nint threadSecurity, bool inherit, uint flags, nint environment, string directory, ref StartupInfo startup, out ProcessInfo process);
}
