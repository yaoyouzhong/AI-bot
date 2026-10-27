using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows.Automation;

// Test of method selection only. Does not accept or submit any credential.
internal static class MethodSwitch
{
    private sealed record Node(AutomationElement Element, int Pid, string Key, int Type, string Kind,
        bool PlainLabel, bool Protected, bool Focused, bool Enabled, bool Offscreen, bool Invokable, int[] Ancestors);
    private sealed record View(List<Node> Nodes, string Method, int Edits, int FocusedEdits);
    private static readonly string Output = Path.Combine(AppContext.BaseDirectory, "selection.jsonl");
    private static readonly object Gate = new();
    private static bool traceEnabled;
    private static Func<bool>? requestValid;

    private static void Guard(int session)
    {
        if (requestValid?.Invoke() == false) throw new InvalidOperationException("request_expired");
        if (Process.GetCurrentProcess().SessionId != session || Native.WTSGetActiveConsoleSessionId() != (uint)session || Native.Locked(session) != true)
            throw new InvalidOperationException("session_changed");
        string threadDesktop = Native.DesktopName(Native.GetThreadDesktop(Native.GetCurrentThreadId()));
        nint input = Native.OpenInputDesktop(0, false, 1);
        if (input == 0) throw new InvalidOperationException("desktop_unavailable");
        try {
            if (!threadDesktop.Equals("Winlogon", StringComparison.OrdinalIgnoreCase) ||
                !Native.DesktopName(input).Equals("Winlogon", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("desktop_changed");
        } finally { Native.CloseDesktop(input); }
    }

    private static bool LogonUi(nint hwnd, int session, out int pid)
    {
        Native.GetWindowThreadProcessId(hwnd, out uint value); pid = (int)value;
        try { using var process = Process.GetProcessById(pid);
            return process.SessionId == session && string.Equals(process.MainModule?.FileName,
                Path.Combine(Environment.SystemDirectory, "LogonUI.exe"), StringComparison.OrdinalIgnoreCase);
        } catch { return false; }
    }

    internal static bool IsLabelContext(IEnumerable<int> ancestors) =>
        !ancestors.Any(type => type == ControlType.Button.Id || type == ControlType.Hyperlink.Id || type == ControlType.Edit.Id || type == ControlType.RadioButton.Id);

    private static int[] Ancestors(AutomationElement element)
    {
        var types = new List<int>();
        var parent = TreeWalker.ControlViewWalker.GetParent(element);
        for (int level = 0; parent is not null && level < 32; level++) {
            var type = (ControlType)parent.GetCurrentPropertyValue(AutomationElement.ControlTypeProperty);
            types.Add(type.Id);
            if (type == ControlType.Window) return types.ToArray();
            parent = TreeWalker.ControlViewWalker.GetParent(parent);
        }
        if (parent is not null) throw new InvalidOperationException("ancestry_limit");
        return types.ToArray();
    }

    private static View Read(int session)
    {
        Guard(session);
        var nodes = new List<Node>(); var keys = new HashSet<string>(); Exception? failure = null;
        bool enumerated = Native.EnumDesktopWindows(Native.GetThreadDesktop(Native.GetCurrentThreadId()), (hwnd, unused) => {
            if (!Native.IsWindowVisible(hwnd) || !LogonUi(hwnd, session, out int pid)) return true;
            try {
                var root = AutomationElement.FromHandle(hwnd);
                var elements = root.FindAll(TreeScope.Descendants, Condition.TrueCondition);
                if (elements.Count > 512) throw new InvalidOperationException("tree_limit");
                for (int i = 0; i < elements.Count; i++) {
                    var element = elements[i];
                    if ((int)element.GetCurrentPropertyValue(AutomationElement.ProcessIdProperty) != pid) continue;
                    string key = string.Join(',', element.GetRuntimeId());
                    if (!keys.Add(key)) continue;
                    var type = (ControlType)element.GetCurrentPropertyValue(AutomationElement.ControlTypeProperty);
                    bool protectedEdit = type == ControlType.Edit && element.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty) is true;
                    // UIA Name is a semantic control label, distinct from Value/Text.
                    // On verified system LogonUI only, classify the protected edit's
                    // label as well; never query its contents or emit raw names.
                    int[] ancestors = type == ControlType.Text ? Ancestors(element) : [];
                    bool plainLabel = type == ControlType.Text && IsLabelContext(ancestors);
                    string kind = protectedEdit || type == ControlType.Button || type == ControlType.Hyperlink || plainLabel
                        ? LoginMethodLabels.Classify(element.GetCurrentPropertyValue(AutomationElement.NameProperty) as string) : "other";
                    if (kind == "other" && !protectedEdit) continue;
                    nodes.Add(new(element, pid, key, type.Id, kind, plainLabel, protectedEdit,
                        element.GetCurrentPropertyValue(AutomationElement.HasKeyboardFocusProperty) is true,
                        element.GetCurrentPropertyValue(AutomationElement.IsEnabledProperty) is true,
                        element.GetCurrentPropertyValue(AutomationElement.IsOffscreenProperty) is true,
                        element.TryGetCurrentPattern(InvokePattern.Pattern, out _), ancestors));
                }
            } catch (Exception ex) { failure = ex; return false; }
            return true;
        }, 0);
        Guard(session);
        if (failure is not null) throw failure;
        if (!enumerated) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        string method = Detect(nodes.Where(n => (n.PlainLabel || n.Protected) && n.Enabled && !n.Offscreen).Select(n => n.Kind));
        var edits = nodes.Where(n => n.Protected && n.Enabled && !n.Offscreen).ToArray();
        return new(nodes, method, edits.Length, edits.Count(n => n.Focused));
    }

    internal static string Detect(IEnumerable<string> labels)
    {
        var methods = labels.Where(x => x is "pin" or "password" or "fingerprint" or "face").Distinct().ToArray();
        return methods.Length == 1 ? methods[0] : "unknown";
    }

    private static bool Transient(Exception ex) => ex is ElementNotAvailableException || ex.HResult == unchecked((int)0x80040201);
    private static void Trace(string stage, int step, string target, View? view = null, int error = 0)
    {
        if (!traceEnabled) return;
        lock (Gate) File.AppendAllText(Output, JsonSerializer.Serialize(new { Utc = DateTimeOffset.UtcNow, Stage = stage, Step = step, Target = target,
            Detected = view?.Method ?? "unknown", Edits = view?.Edits ?? 0, FocusedEdits = view?.FocusedEdits ?? 0, Error = error,
            Controls = view?.Nodes.Select(n => new { n.Type, n.Kind, n.PlainLabel, n.Protected, n.Focused, n.Enabled, n.Offscreen, n.Invokable, n.Ancestors }).ToArray() }) + Environment.NewLine);
    }

    private static void Invoke(int session, Node target)
    {
        Guard(session);
        if ((int)target.Element.GetCurrentPropertyValue(AutomationElement.ProcessIdProperty) != target.Pid ||
            !target.Element.Current.IsEnabled || target.Element.Current.IsOffscreen ||
            LoginMethodLabels.Classify(target.Element.GetCurrentPropertyValue(AutomationElement.NameProperty) as string) != target.Kind)
            throw new InvalidOperationException("target_changed");
        if (!target.Element.TryGetCurrentPattern(InvokePattern.Pattern, out var pattern)) throw new InvalidOperationException("invoke_unavailable");
        ((InvokePattern)pattern).Invoke();
    }

    private static bool Select(int session, int step, string target, int limitMs = 10000)
    {
        bool optionsInvoked = false, targetInvoked = false, focusRequested = false; int stable = 0;
        var deadline = Stopwatch.StartNew();
        while (deadline.ElapsedMilliseconds < limitMs) {
            View view;
            try { view = Read(session); }
            catch (Exception ex) when (Transient(ex)) { Guard(session); stable = 0; Trace("transition-recheck", step, target, error: ex.HResult); Thread.Sleep(200); continue; }
            Trace("selection-observed", step, target, view);
            if (view.Method == target && view.Edits == 1) {
                if (view.FocusedEdits == 1) {
                    if (++stable >= 3) { Guard(session); Trace("method-ready", step, target, view); return true; }
                } else {
                    stable = 0;
                    if (!focusRequested) {
                        focusRequested = true; Guard(session);
                        var edit = view.Nodes.Single(n => n.Protected && n.Enabled && !n.Offscreen);
                        edit.Element.SetFocus();
                        Trace("focus-requested", step, target, view);
                    }
                }
                Thread.Sleep(200); continue;
            } else stable = 0;
            if (!targetInvoked) {
                var targets = view.Nodes.Where(n => n.Type == ControlType.Button.Id && n.Kind == target && n.Enabled && !n.Offscreen && n.Invokable).ToArray();
                if (targets.Length > 1) { Trace("ambiguous-target", step, target, view); return false; }
                if (targets.Length == 1) {
                    targetInvoked = true; // Consume before invoking: uncertain actions are never repeated.
                    Trace("target-invoke-start", step, target, view);
                    Invoke(session, targets[0]);
                    Trace("target-invoke-returned", step, target);
                } else if (!optionsInvoked) {
                    var options = view.Nodes.Where(n => n.Kind == "options" && n.Type is 50000 or 50005 && n.Enabled && !n.Offscreen && n.Invokable).ToArray();
                    if (options.Length > 1) { Trace("ambiguous-options", step, target, view); return false; }
                    if (options.Length == 1) { optionsInvoked = true; Invoke(session, options[0]); Trace("options-invoked", step, target, view); }
                }
            }
            Thread.Sleep(200);
        }
        Trace("selection-timeout", step, target); return false;
    }

    internal static int Run(int session)
    {
        traceEnabled = true;
        using var watchdog = new Timer(_ => { Trace("selection-watchdog", 0, "none", error: 1460); Environment.Exit(124); }, null, 75000, Timeout.Infinite);
        Guard(session); Trace("selection-started-no-credentials", 0, "none");
        // Allow manual wallpaper dismissal and initial fingerprint selection.
        Thread.Sleep(6000);
        string[] targets = ["pin", "password", "pin", "password", "pin"];
        for (int i = 0; i < targets.Length; i++) {
            try { if (!Select(session, i + 1, targets[i])) return 6; }
            catch (Exception ex) { Trace("selection-stopped", i + 1, targets[i], error: ex.HResult); return 7; }
            Thread.Sleep(3000);
        }
        Trace("selection-completed-no-credentials", targets.Length, "pin"); return 0;
    }

    internal static bool Prepare(int session, string target, Func<bool> valid)
    {
        if (target is not ("pin" or "password")) return false;
        requestValid = valid;
        try { return Select(session, 1, target, 3500); }
        finally { requestValid = null; }
    }

    internal static void SelfTest()
    {
        if (Detect(["options", "pin", "pin"]) != "pin" || Detect(["password"]) != "password" ||
            Detect(["pin", "password"]) != "unknown" || Detect(["options"]) != "unknown") throw new Exception("Method ambiguity guard failed");
        // A credential tile can be a ListItem. Its label identifies its method;
        // labels inside method buttons or protected edits must not count.
        if (!IsLabelContext([ControlType.Group.Id, ControlType.ListItem.Id, ControlType.Window.Id]) ||
            IsLabelContext([ControlType.Button.Id, ControlType.ListItem.Id]) ||
            IsLabelContext([ControlType.Edit.Id, ControlType.ListItem.Id])) throw new Exception("Label ownership guard failed");
    }
}
