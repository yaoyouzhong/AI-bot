using System.Runtime.InteropServices;

namespace AIBotBridge;

internal static class SystemIdleTime
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LastInputInfo
    {
        internal uint Size;
        internal uint Time;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetLastInputInfo(ref LastInputInfo info);

    internal static TimeSpan Read()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        var current = unchecked((uint)Environment.TickCount);
        var elapsed = unchecked(current - info.Time);
        return TimeSpan.FromMilliseconds(elapsed);
    }
}
