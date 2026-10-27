using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;

// Feasibility experiment only: no credential reads, input, network or settings.
internal static class Program
{
    private static readonly string Output = Path.Combine(AppContext.BaseDirectory, "observations.jsonl");
    private static readonly object Gate = new();
    private sealed record Control(int Type, string Label, string IdHash, string IdHint,
        bool Protected, bool Focused, bool Enabled, bool Offscreen, bool Invoke, bool Select, bool? Selected);
    private sealed record Sample(string Stage, int Session, bool? Locked, string Desktop,
        int Windows = 0, Control[]? Controls = null, int Error = 0);

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.SequenceEqual(new[] { "--self-test" })) return SelfTest();
        if (args.Length is < 2 or > 3 || (args.Length == 3 && args[2] != "cycle") || args[0] is not ("--launch" or "--inspect") ||
            !int.TryParse(args[1], out int session) || session <= 0 || !WindowsIdentity.GetCurrent().IsSystem) return 77;
        bool cycle = args.Length == 3;
        try { return args[0] == "--launch" ? Launch(session, cycle) : cycle ? MethodSwitch.Run(session) : Inspect(session); }
        catch (Exception ex) { Write(new("probe-error", session, Native.Locked(session), "unknown", Error: ex.HResult)); return 1; }
    }

    internal static string Label(string? value) => value?.Trim().ToLowerInvariant() switch {
        "登录选项" or "登陆选项" or "sign-in options" or "sign in options" => "options",
        "密码" or "password" or "使用密码登录" or "sign in with your password" => "password",
        "pin" or "pin (windows hello)" or "windows hello pin" or "pin 码" or "pin码" or "使用 pin 登录" => "pin",
        "人脸" or "人脸识别" or "人脸识别 (windows hello)" or "face" or "facial recognition" or "windows hello face" => "face",
        "指纹" or "指纹识别" or "指纹识别 (windows hello)" or "fingerprint" or "fingerprint recognition" or "windows hello fingerprint" => "fingerprint",
        _ => "other"
    };

    private static string IdHint(string id) => id.Contains("password", StringComparison.OrdinalIgnoreCase) ? "password-like" :
        id.Contains("pin", StringComparison.OrdinalIgnoreCase) ? "pin-like" : "other";
    private static bool Eligible(int current, int target, uint active, bool? locked) => current == target && active == (uint)target && locked == true;
    private static bool SameSession(int session) => Eligible(Process.GetCurrentProcess().SessionId, session, Native.WTSGetActiveConsoleSessionId(), Native.Locked(session));
    private static bool IsLogonUi(nint hwnd, int session, out int pid)
    {
        Native.GetWindowThreadProcessId(hwnd, out uint value); pid = (int)value;
        try { using var process = Process.GetProcessById(pid);
            return process.SessionId == session && string.Equals(process.MainModule?.FileName,
                Path.Combine(Environment.SystemDirectory, "LogonUI.exe"), StringComparison.OrdinalIgnoreCase);
        } catch { return false; }
    }

    private static int Inspect(int session)
    {
        using var watchdog = new Timer(_ => { Write(new("watchdog-timeout", session, Native.Locked(session), "unknown", Error: 1460)); Environment.Exit(124); }, null, 75000, Timeout.Infinite);
        string desktop = Native.DesktopName(Native.GetThreadDesktop(Native.GetCurrentThreadId()));
        if (!SameSession(session) || !desktop.Equals("Winlogon", StringComparison.OrdinalIgnoreCase)) return 3;
        Write(new("observation-started-no-input", session, true, desktop));
        var elapsed = Stopwatch.StartNew();
        while (elapsed.Elapsed < TimeSpan.FromSeconds(55)) {
            if (!SameSession(session)) { Write(new("session-changed", session, Native.Locked(session), desktop)); return 3; }
            var controls = new List<Control>(); int windows = 0, error = 0;
            bool enumerated = Native.EnumDesktopWindows(Native.GetThreadDesktop(Native.GetCurrentThreadId()), (hwnd, unused) => {
                if (!Native.IsWindowVisible(hwnd) || !IsLogonUi(hwnd, session, out int pid)) return true;
                windows++;
                try {
                    var root = AutomationElement.FromHandle(hwnd);
                    var elements = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                    for (int i = 0; i < Math.Min(elements.Count, 512); i++) {
                        var element = elements[i];
                        if ((int)element.GetCurrentPropertyValue(AutomationElement.ProcessIdProperty) != pid) continue;
                        var type = (ControlType)element.GetCurrentPropertyValue(AutomationElement.ControlTypeProperty);
                        bool password = element.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty) is true;
                        // Never request Name on an edit, or Value/Text on any element.
                        string label = !password && (type == ControlType.Button || type == ControlType.Hyperlink || type == ControlType.Text || type == ControlType.RadioButton || type == ControlType.ListItem)
                            ? Label(element.GetCurrentPropertyValue(AutomationElement.NameProperty) as string) : "other";
                        if (label == "other" && type != ControlType.Edit) continue;
                        string id = element.GetCurrentPropertyValue(AutomationElement.AutomationIdProperty) as string ?? "";
                        string hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id)))[..12];
                        bool selectable = element.TryGetCurrentPattern(SelectionItemPattern.Pattern, out var selection);
                        controls.Add(new(type.Id, label, hash, IdHint(id), password,
                            element.GetCurrentPropertyValue(AutomationElement.HasKeyboardFocusProperty) is true,
                            element.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty) is true,
                            element.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty) is true,
                            element.TryGetCurrentPattern(InvokePattern.Pattern, out _), selectable,
                            selectable ? ((SelectionItemPattern)selection).Current.IsSelected : null));
                    }
                } catch (Exception ex) { error = ex.HResult; return false; }
                return windows < 8;
            }, 0);
            if (!enumerated && error == 0) error = Marshal.GetLastWin32Error();
            if (!SameSession(session)) { Write(new("session-changed", session, Native.Locked(session), desktop)); return 3; }
            Write(new("observed-no-input", session, true, desktop, windows, controls.ToArray(), error));
            if (error != 0) return 5;
            Thread.Sleep(750);
        }
        Write(new("observation-completed-no-input", session, Native.Locked(session), desktop));
        return 0;
    }

    private static int Launch(int session, bool cycle)
    {
        if (Process.GetCurrentProcess().SessionId != 0) return 77;
        var wait = Stopwatch.StartNew();
        while (wait.Elapsed < TimeSpan.FromSeconds(180)) {
            if (Native.WTSGetActiveConsoleSessionId() != (uint)session) return 3;
            if (Native.Locked(session) == true) break;
            Thread.Sleep(250);
        }
        if (Native.Locked(session) != true) { Write(new("waiting-for-lock-timeout", session, Native.Locked(session), "session0")); return 4; }
        Native.Check(Native.OpenProcessToken(Native.GetCurrentProcess(), 0x2B, out nint token));
        try {
            Native.EnableTcb(token);
            Native.Check(Native.DuplicateTokenEx(token, 0x1AF, 0, 2, 1, out nint duplicate));
            try {
                Native.Check(Native.SetTokenInformation(duplicate, 12, ref session, sizeof(int)));
                string exe = Environment.ProcessPath!;
                var start = new Native.StartupInfo { Size = Marshal.SizeOf<Native.StartupInfo>(), Desktop = @"WinSta0\Winlogon" };
                Native.Check(Native.CreateProcessAsUser(duplicate, exe, new StringBuilder($"\"{exe}\" --inspect {session}" + (cycle ? " cycle" : "")), 0, 0, false, 0x08000000, 0, AppContext.BaseDirectory, ref start, out var child));
                try {
                    if (Native.WaitForSingleObject(child.Process, 80000) != 0) { Native.TerminateProcess(child.Process, 124); Native.WaitForSingleObject(child.Process, 3000); return 124; }
                    Native.Check(Native.GetExitCodeProcess(child.Process, out uint code)); return (int)code;
                } finally { Native.CloseHandle(child.Thread); Native.CloseHandle(child.Process); }
            } finally { Native.CloseHandle(duplicate); }
        } finally { Native.CloseHandle(token); }
    }

    private static void Write(Sample sample)
    {
        lock (Gate) File.AppendAllText(Output, JsonSerializer.Serialize(new { Utc = DateTimeOffset.UtcNow, Sample = sample }) + Environment.NewLine);
    }

    private static int SelfTest()
    {
        if (Label("PIN") != "pin" || Label("密码") != "password" || Label("Sign-in options") != "options" ||
            Label("1234") != "other" || Label("not a PIN") != "other" || Label("person@example.com") != "other") throw new Exception("Label classifier failed");
        if (!Eligible(1, 1, 1, true) || Eligible(0, 1, 1, true) || Eligible(1, 2, 1, true) || Eligible(1, 1, 2, true) || Eligible(1, 1, 1, null) || Eligible(1, 1, 1, false)) throw new Exception("Session guard failed");
        MethodSwitch.SelfTest();
        Console.WriteLine("LOGIN_METHOD_PROBE_SELF_TEST_OK (classification/session guards only; real observation pending)"); return 0;
    }
}
