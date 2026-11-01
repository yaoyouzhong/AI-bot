using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;

// Read-only prerequisite probe. No credential input, keyboard injection, network
// listener, credential provider, clipboard, screenshot, UI Name or Value reads.
internal static class Program
{
    private static readonly string ResultPath = Path.Combine(AppContext.BaseDirectory, "result.json");
    private static readonly object ReportGate = new();
    private static bool reported;
    private sealed record Report(string Stage, int Session, bool System, bool? Locked,
        string Desktop, bool InputDesktopAccessible, bool ForegroundLogonUi,
        int LogonUiWindows = 0, int ProtectedEdits = 0, int FocusedProtectedEdits = 0,
        int Error = 0, bool UnlockTested = false, int InspectionAttempts = 0);

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.SequenceEqual(new[] { "--self-test" })) return SelfTest();
        if (args.SequenceEqual(new[] { "--preflight" })) {
            var report = Capture("user-preflight");
            Console.WriteLine(JsonSerializer.Serialize(report));
            return 0;
        }
        if (args.Length != 2 || !int.TryParse(args[1], out int session) || session <= 0 ||
            args[0] is not ("--system-launch" or "--inspect")) return 64;
        if (!WindowsIdentity.GetCurrent().IsSystem) return 77;
        try { return args[0] == "--system-launch" ? Launch(session) : Inspect(session); }
        catch (Exception e) {
            Write(new Report("probe-error", session, true, null, "unknown", false, false, Error: e.HResult));
            return 1;
        }
    }

    private static bool Eligible(int current, int requested, uint active, bool? locked) =>
        current == requested && active == (uint)requested && locked == true;

    private static Report Capture(string stage)
    {
        int session = Process.GetCurrentProcess().SessionId;
        string desktop = Native.DesktopName(Native.GetThreadDesktop(Native.GetCurrentThreadId()));
        nint input = Native.OpenInputDesktop(0, false, 1); // DESKTOP_READOBJECTS only
        int error = input == 0 ? Marshal.GetLastWin32Error() : 0;
        if (input != 0) Native.CloseDesktop(input);
        return new Report(stage, session, WindowsIdentity.GetCurrent().IsSystem,
            Native.Locked(session), desktop, input != 0, IsLogonUi(Native.GetForegroundWindow(), session), Error: error);
    }

    private static bool IsLogonUi(nint window, int session)
    {
        if (window == 0) return false;
        Native.GetWindowThreadProcessId(window, out uint pid);
        try {
            using var p = Process.GetProcessById((int)pid);
            return p.SessionId == session && string.Equals(p.MainModule?.FileName,
                Path.Combine(Environment.SystemDirectory, "LogonUI.exe"), StringComparison.OrdinalIgnoreCase);
        } catch { return false; }
    }

    private static int Inspect(int session)
    {
        // Independent watchdog: UI Automation may block inside another process.
        using var timeout = new System.Threading.Timer(_ => {
            Write(new Report("inspection-timeout", session, true, null, "unknown", false, false, Error: 1460));
            Environment.Exit(124);
        }, null, 12000, Timeout.Infinite);
        var first = Capture("secure-desktop-structure");
        if (!Eligible(first.Session, session, Native.WTSGetActiveConsoleSessionId(), first.Locked) ||
            !first.Desktop.Equals("Winlogon", StringComparison.OrdinalIgnoreCase) || !first.InputDesktopAccessible) {
            Write(first with { Stage = "secure-desktop-unavailable" }); return 2;
        }
        int windows = 0, edits = 0, focused = 0, inspectionError = 0, attempts = 0;
        // Lock notification may precede LogonUI/PIN controls becoming visible.
        // Give the user a bounded opportunity to dismiss the lock wallpaper.
        var inspectionWindow = Stopwatch.StartNew();
        do {
        var current = Capture("waiting-for-login-controls");
        if (!Eligible(current.Session, session, Native.WTSGetActiveConsoleSessionId(), current.Locked)) {
            Write(current with { Stage = "session-changed", InspectionAttempts = attempts }); return 3;
        }
        windows = edits = focused = 0;
        attempts++;
        bool enumerated = Native.EnumDesktopWindows(Native.GetThreadDesktop(Native.GetCurrentThreadId()), (hwnd, _) => {
            try {
            if (!IsLogonUi(hwnd, session) || !Native.IsWindowVisible(hwnd)) return true;
            windows++;
            var root = AutomationElement.FromHandle(hwnd);
            var elements = root.FindAll(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            for (int i = 0; i < Math.Min(elements.Count, 128); i++) {
                // Only individually selected non-content properties. Never read
                // Current.Name, ValuePattern, TextPattern or UI element dumps.
                var element = elements[i];
                if (element.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty) is true) {
                    edits++;
                    if (element.GetCurrentPropertyValue(AutomationElement.HasKeyboardFocusProperty) is true) focused++;
                }
            }
            return windows < 16;
            } catch (Exception e) { inspectionError = e.HResult; return false; }
        }, 0);
        if (!enumerated && inspectionError == 0 && windows < 16) inspectionError = Marshal.GetLastWin32Error();
        if (inspectionError != 0 || edits > 0) break;
        Thread.Sleep(250);
        } while (inspectionWindow.Elapsed < TimeSpan.FromSeconds(10));
        var last = Capture("structure-inspected-not-unlock");
        if (!Eligible(last.Session, session, Native.WTSGetActiveConsoleSessionId(), last.Locked)) {
            Write(last with { Stage = "session-changed" }); return 3;
        }
        Write(last with { Stage = inspectionError == 0 ? last.Stage : "structure-inspection-failed",
            LogonUiWindows = windows, ProtectedEdits = edits, FocusedProtectedEdits = focused,
            Error = inspectionError, InspectionAttempts = attempts });
        return inspectionError == 0 ? 0 : 5;
    }

    private static int Launch(int session)
    {
        if (Process.GetCurrentProcess().SessionId != 0) return 77;
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < TimeSpan.FromSeconds(120)) {
            if (Native.WTSGetActiveConsoleSessionId() != (uint)session) {
                Write(new Report("console-session-changed", session, true, null, "session0", false, false)); return 3;
            }
            if (Native.Locked(session) == true) break;
            Thread.Sleep(250);
        }
        if (Native.Locked(session) != true) {
            Write(new Report("waiting-for-lock-timeout", session, true, Native.Locked(session), "session0", false, false)); return 4;
        }
        // Copy only our own SYSTEM token. No access to another process token.
        Native.Check(Native.OpenProcessToken(Native.GetCurrentProcess(), 0x2B, out nint token));
        try {
            Native.EnableTcb(token);
            Native.Check(Native.DuplicateTokenEx(token, 0x1AF, 0, 2, 1, out nint childToken));
            try {
                Native.Check(Native.SetTokenInformation(childToken, 12, ref session, sizeof(int)));
                string exe = Environment.ProcessPath!;
                var start = new Native.StartupInfo { Size = Marshal.SizeOf<Native.StartupInfo>(), Desktop = @"WinSta0\Winlogon" };
                Native.Check(Native.CreateProcessAsUser(childToken, exe,
                    new StringBuilder($"\"{exe}\" --inspect {session}"), 0, 0, false,
                    0x08000000, 0, AppContext.BaseDirectory, ref start, out var child));
                try {
                    uint waited = Native.WaitForSingleObject(child.Process, 16000);
                    if (waited != 0) {
                        Native.TerminateProcess(child.Process, 124);
                        Native.WaitForSingleObject(child.Process, 3000);
                        if (!File.Exists(ResultPath)) Write(new Report("child-timeout", session, true, null, "unknown", false, false, Error: 1460));
                        return 124;
                    }
                    Native.Check(Native.GetExitCodeProcess(child.Process, out uint exit));
                    return (int)exit;
                } finally { Native.CloseHandle(child.Thread); Native.CloseHandle(child.Process); }
            } finally { Native.CloseHandle(childToken); }
        } finally { Native.CloseHandle(token); }
    }

    private static void Write(Report report)
    {
        lock (ReportGate) {
            if (reported) return;
            // Installer supplies a fresh, administrator-owned directory each run.
            using var output = new FileStream(ResultPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            JsonSerializer.Serialize(output, report, new JsonSerializerOptions { WriteIndented = true });
            reported = true;
        }
    }

    private static int SelfTest()
    {
        if (!Eligible(1, 1, 1, true) || Eligible(0, 1, 1, true) || Eligible(1, 2, 1, true) ||
            Eligible(1, 1, 2, true) || Eligible(1, 1, 1, false) || Eligible(1, 1, 1, null)) throw new Exception("Session guard failed");
        var serialized = JsonSerializer.Serialize(new Report("test", 1, false, true, "Winlogon", true, true));
        if (serialized.Contains("Value") || serialized.Contains("Password") || !serialized.Contains("\"UnlockTested\":false")) throw new Exception("Report contract failed");
        Console.WriteLine("WIFI_UNLOCK_PROBE_SELF_TEST_OK (guards/report only; no secure desktop or unlock tested)");
        return 0;
    }
}

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
