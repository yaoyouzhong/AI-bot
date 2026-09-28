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

    // Stable Windows uptime timestamp, not a wall-clock estimate. No key contents collected.
    internal static long? LastInputTickMilliseconds()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return null;
        return ExpandInputTick(Environment.TickCount64, info.Time);
    }

    internal static long ExpandInputTick(long current, uint input) =>
        current - unchecked((uint)current - input);

    internal static TimeSpan Read()
    {
        var info = new LastInputInfo { Size = (uint)Marshal.SizeOf<LastInputInfo>() };
        if (!GetLastInputInfo(ref info)) return TimeSpan.Zero;
        var current = unchecked((uint)Environment.TickCount);
        var elapsed = unchecked(current - info.Time);
        return TimeSpan.FromMilliseconds(elapsed);
    }
}
