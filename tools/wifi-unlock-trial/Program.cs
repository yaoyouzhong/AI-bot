using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Windows.Automation;
using Microsoft.Win32.SafeHandles;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.SequenceEqual(new[] { "--self-test" })) return TrialTests.Run();
        if (args.Length != 2 || !int.TryParse(args[1], out int session) || session <= 0 ||
            args[0] is not ("--system-launch" or "--serve")) return 64;
        if (!WindowsIdentity.GetCurrent().IsSystem) return 77;
        try { return args[0] == "--system-launch" ? Launch(session) : Serve(session); }
        catch (Exception ex) { Report(new { stage = "trial-error", error = ex.HResult, unlocked = false }); return 1; }
    }
    private static void Report(object result)
    {
        // Numeric/status fields only. Never exception text, UI content or requests.
        using var stream = new FileStream(Path.Combine(AppContext.BaseDirectory, "result.json"), FileMode.CreateNew);
        JsonSerializer.Serialize(stream, result);
    }
    private static bool LockedSession(int session) => Native.WTSGetActiveConsoleSessionId() == session && Native.Locked(session) == true;
    private static int Launch(int session)
    {
        if (Process.GetCurrentProcess().SessionId != 0) return 77;
        var timer = Stopwatch.StartNew();
        while (timer.Elapsed.TotalSeconds < 90 && !LockedSession(session)) {
            if (Native.WTSGetActiveConsoleSessionId() != session) return 3;
            Thread.Sleep(250);
        }
        if (!LockedSession(session)) { Report(new { stage = "lock-timeout", unlocked = false }); return 4; }
        Native.Check(Native.OpenProcessToken(Native.GetCurrentProcess(), 0x2B, out nint token));
        try {
            Native.EnableTcb(token);
            Native.Check(Native.DuplicateTokenEx(token, 0x1AF, 0, 2, 1, out nint childToken));
            try {
                Native.Check(Native.SetTokenInformation(childToken, 12, ref session, sizeof(int)));
                var start = new Native.StartupInfo { Size = Marshal.SizeOf<Native.StartupInfo>(), Desktop = @"WinSta0\Winlogon" };
                Native.Check(Native.CreateProcessAsUser(childToken, Environment.ProcessPath!,
                    new StringBuilder($"\"{Environment.ProcessPath}\" --serve {session}"), 0, 0, false, 0x08000000,
                    0, AppContext.BaseDirectory, ref start, out var child));
                try {
                    if (Native.WaitForSingleObject(child.Process, 135000) != 0) {
                        Native.TerminateProcess(child.Process, 124); Native.WaitForSingleObject(child.Process, 3000);
                        if (!File.Exists(Path.Combine(AppContext.BaseDirectory, "result.json"))) Report(new { stage = "worker-timeout", unlocked = false });
                        return 124;
                    }
                    Native.Check(Native.GetExitCodeProcess(child.Process, out uint code)); return (int)code;
                } finally { Native.CloseHandle(child.Thread); Native.CloseHandle(child.Process); }
            } finally { Native.CloseHandle(childToken); }
        } finally { Native.CloseHandle(token); }
    }
    private static int Serve(int session)
    {
        if (Process.GetCurrentProcess().SessionId != session || !LockedSession(session) ||
            Native.DesktopName(Native.GetThreadDesktop(Native.GetCurrentThreadId())) != "Winlogon") return 77;
        Native.Check(Extra.WTSQueryUserToken((uint)session, out nint userToken));
        SecurityIdentifier sid;
        try { using var identity = new WindowsIdentity(userToken); sid = identity.User!; }
        finally { Native.CloseHandle(userToken); }
        var security = new PipeSecurity(); security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new PipeAccessRule(new SecurityIdentifier("S-1-5-18"), PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        bool submitted = false, unlocked = false; string? expectedTarget = null;
        bool SameTarget() {
            string? current = Target(session); if (current is null) return false;
            expectedTarget ??= current; return current == expectedTarget;
        }
        using var protocol = new TrialProtocol(SameTarget, pin => {
            submitted = true; unlocked = InjectOnce(session, pin, SameTarget); return unlocked;
        });
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(120));
        // An unresponsive UI Automation provider cannot leave a privileged helper alive.
        using var watchdog = new System.Threading.Timer(_ => Environment.Exit(124), null, 130000, Timeout.Infinite);
        int requests = 0;
        while (!stop.IsCancellationRequested && !protocol.Attempted && requests++ < 16) {
            using var pipe = NamedPipeServerStreamAcl.Create($"AIBot.WifiUnlockTrial.Session{session}", PipeDirection.InOut,
                1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous, 2048, 2048, security);
            try {
                pipe.WaitForConnectionAsync(stop.Token).GetAwaiter().GetResult();
                if (!Extra.GetNamedPipeClientSessionId(pipe.SafePipeHandle, out uint clientSession) || clientSession != session) continue;
                using var requestTimeout = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                requestTimeout.CancelAfter(15000);
                var token = requestTimeout.Token;
                byte[] header = new byte[4]; pipe.ReadExactlyAsync(header, token).AsTask().GetAwaiter().GetResult();
                int length = BitConverter.ToInt32(header);
                if (length is < 2 or > 2048) continue;
                byte[] request = new byte[length];
                try {
                    pipe.ReadExactlyAsync(request, token).AsTask().GetAwaiter().GetResult();
                    using var doc = JsonDocument.Parse(request);
                    byte[] response = JsonSerializer.SerializeToUtf8Bytes(protocol.Handle(doc.RootElement));
                    try {
                        pipe.WriteAsync(BitConverter.GetBytes(response.Length), token).AsTask().GetAwaiter().GetResult();
                        pipe.WriteAsync(response, token).AsTask().GetAwaiter().GetResult();
                        pipe.FlushAsync(token).GetAwaiter().GetResult();
                    } finally { CryptographicOperations.ZeroMemory(response); }
                } finally { CryptographicOperations.ZeroMemory(request); }
            } catch (Exception ex) when (ex is IOException or OperationCanceledException or JsonException) {
                // No reconnect/replay of a submission. Attempted remains consumed.
            }
        }
        Report(new { stage = "trial-finished", session, submitted, unlocked, attempted = protocol.Attempted });
        return unlocked ? 0 : 2;
    }
    private static string? Target(int session)
    {
        if (!LockedSession(session)) return null;
        nint hwnd = Native.GetForegroundWindow(); if (hwnd == 0) return null;
        Native.GetWindowThreadProcessId(hwnd, out uint pid);
        try {
            using var process = Process.GetProcessById((int)pid);
            if (process.SessionId != session || !string.Equals(process.MainModule?.FileName,
                Path.Combine(Environment.SystemDirectory, "LogonUI.exe"), StringComparison.OrdinalIgnoreCase)) return null;
            var focused = AutomationElement.FocusedElement;
            bool valid = focused is not null && focused.GetCurrentPropertyValue(AutomationElement.ProcessIdProperty) is int owner && owner == pid &&
                focused.GetCurrentPropertyValue(AutomationElement.ControlTypeProperty) == ControlType.Edit &&
                focused.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty) is true &&
                focused.GetCurrentPropertyValue(AutomationElement.HasKeyboardFocusProperty) is true;
            return valid ? $"{pid}:{hwnd}:{string.Join(',', focused!.GetRuntimeId())}" : null;
        } catch { return null; }
    }
    private static bool InjectOnce(int session, byte[] pin, Func<bool> sameTarget)
    {
        if (!sameTarget() || Extra.ModifierHeld()) return false;
        // Clear any prior user input without reading it. Recheck before sending digits.
        if (!Extra.SendKeys([Extra.Key(0x11), Extra.Key(0x41), Extra.Key(0x41, true), Extra.Key(0x11, true), Extra.Key(0x08), Extra.Key(0x08, true)])) return false;
        if (!sameTarget() || Extra.ModifierHeld()) return false;
        var inputs = new Extra.Input[pin.Length * 2];
        try {
            for (int i = 0; i < pin.Length; i++) {
                inputs[i * 2] = Extra.Unicode(pin[i], false); inputs[i * 2 + 1] = Extra.Unicode(pin[i], true);
            }
            if (!Extra.SendKeys(inputs)) return false;
        } finally { Array.Clear(inputs); }
        // Some Hello PIN providers submit automatically; do not send Enter onto
        // an unlocked desktop. Otherwise submit once after checking the target again.
        for (int i = 0; i < 4; i++) { Thread.Sleep(100); if (Native.Locked(session) == false) return Native.WTSGetActiveConsoleSessionId() == session; }
        if (!sameTarget() || Extra.ModifierHeld() || !Extra.SendKeys([Extra.Key(0x0D), Extra.Key(0x0D, true)])) return false;
        for (int i = 0; i < 80; i++) {
            if (Native.WTSGetActiveConsoleSessionId() != session) return false;
            if (Native.Locked(session) == false) return true;
            Thread.Sleep(100);
        }
        return false;
    }
}
internal static class Extra
{
    [StructLayout(LayoutKind.Sequential)] internal struct Keyboard { public ushort Vk, Scan; public uint Flags, Time; public nuint Info; }
    [StructLayout(LayoutKind.Explicit, Size = 32)] internal struct Union { [FieldOffset(0)] public Keyboard Keyboard; }
    [StructLayout(LayoutKind.Sequential)] internal struct Input { public uint Type; public Union Data; }
    internal static Input Key(ushort vk, bool up = false) => new() { Type = 1, Data = new() { Keyboard = new() { Vk = vk, Flags = up ? 2u : 0 } } };
    internal static Input Unicode(byte value, bool up) => new() { Type = 1, Data = new() { Keyboard = new() { Scan = value, Flags = 4u | (up ? 2u : 0) } } };
    internal static bool SendKeys(Input[] keys) => SendInput((uint)keys.Length, keys, Marshal.SizeOf<Input>()) == keys.Length;
    internal static bool ModifierHeld() => new[] { 0x10, 0x11, 0x12, 0x5B, 0x5C }.Any(key => (GetAsyncKeyState(key) & 0x8000) != 0);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll", SetLastError = true)] private static extern uint SendInput(uint count, Input[] inputs, int size);
    [DllImport("wtsapi32.dll", SetLastError = true)] internal static extern bool WTSQueryUserToken(uint session, out nint token);
    [DllImport("kernel32.dll", SetLastError = true)] internal static extern bool GetNamedPipeClientSessionId(SafePipeHandle pipe, out uint session);
}
