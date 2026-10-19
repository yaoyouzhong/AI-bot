using System.Runtime.InteropServices;

namespace AIBotBridge;

// Ordinary interactive-session notifications only. No power policy changes,
// input injection, monitor polling, elevated helper, or persisted state.
internal sealed class WindowsDisplayPower : NativeWindow, IDisposable
{
    private static readonly Guid SessionDisplay = new("2B84C20E-AD23-4DDF-93DB-05FFBD7EFCA5");
    private static int _state = -1;
    private IntPtr _registration;
    internal static int? Decode(int value) => value is >= 0 and <= 2 ? value : null;
    internal static int? Read() => Decode(Volatile.Read(ref _state));

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(IntPtr recipient, in Guid setting, uint flags);
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr registration);

    internal WindowsDisplayPower()
    {
        Volatile.Write(ref _state, -1);
        try
        {
            CreateHandle(new CreateParams { Caption = "AI-bot display state", Width = 0, Height = 0 });
            _registration = RegisterPowerSettingNotification(Handle, in SessionDisplay, 0);
            if (_registration == IntPtr.Zero) Dispose();
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            Dispose(); // An unavailable display state must not stop the bridge.
        }
    }

    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x218)
        {
            if (message.WParam.ToInt64() == 0x8013 && message.LParam != IntPtr.Zero &&
                Marshal.PtrToStructure<Guid>(message.LParam) == SessionDisplay &&
                Marshal.ReadInt32(message.LParam, 16) == 4)
                Volatile.Write(ref _state, Decode(Marshal.ReadInt32(message.LParam, 20)) ?? -1);
            else if (message.WParam.ToInt64() == 4) // PBT_APMSUSPEND: never retain a pre-suspend On.
                Volatile.Write(ref _state, -1);
        }
        base.WndProc(ref message);
    }

    public void Dispose()
    {
        Volatile.Write(ref _state, -1);
        if (_registration != IntPtr.Zero) UnregisterPowerSettingNotification(_registration);
        _registration = IntPtr.Zero;
        if (Handle != IntPtr.Zero) DestroyHandle();
    }
}
