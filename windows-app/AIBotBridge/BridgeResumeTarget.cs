using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace AIBotBridge;

// Identify the resident bridge by its loopback listener, not by whichever copy
// happens to host the flashing window. No firewall changes or network probes.
internal sealed record BridgeResumeTarget(int ProcessId, DateTime Started, string Executable, int HttpPort)
{
    internal static BridgeResumeTarget Capture()
    {
        using var current = Process.GetCurrentProcess();
        var candidates = new List<BridgeResumeTarget>();
        foreach (var listener in ReadListeners().Where(row => row.Address == 0x0100007f))
        {
            if (listener.Pid == Environment.ProcessId) continue;
            try {
                using var process = Process.GetProcessById(listener.Pid);
                if (process.SessionId != current.SessionId || process.ProcessName != "AIBotBridge") continue;
                var path = process.MainModule?.FileName;
                if (path is not null) candidates.Add(new(process.Id, process.StartTime.ToUniversalTime(), path, listener.Port));
            } catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or Win32Exception) { }
        }
        return Select(candidates);
    }

    internal static BridgeResumeTarget Select(IEnumerable<BridgeResumeTarget> candidates)
    {
        var targets = candidates.Distinct().ToArray();
        if (targets.Length != 1) throw new IOException("暂时无法确认正在运行的桥接程序，未开始刷机。请关闭多余桥接后重试。");
        return targets[0];
    }

    internal ProcessStartInfo StartInfo()
    {
        var start = new ProcessStartInfo(Executable) { UseShellExecute = false, CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(Executable)! };
        start.Environment["AIBOT_HTTP_PORT"] = HttpPort.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return start;
    }

    internal void Restore()
    {
        try {
            using var original = Process.GetProcessById(ProcessId);
            if (!original.HasExited && original.StartTime.ToUniversalTime() == Started) return;
        } catch (ArgumentException) { }
        if (!File.Exists(Executable)) throw new IOException("原桥接程序已被移动，请手动启动桥接。");
        using var restarted = Process.Start(StartInfo());
    }

    private static IEnumerable<(uint Address, int Port, int Pid)> ReadListeners()
    {
        var size = 0;
        var result = GetExtendedTcpTable(IntPtr.Zero, ref size, false, 2, 3, 0); // AF_INET, OWNER_PID_LISTENER
        if (result != 122 && result != 0) throw new Win32Exception((int)result);
        // Listener count may change between size query and table read.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var buffer = Marshal.AllocHGlobal(size);
            try {
                result = GetExtendedTcpTable(buffer, ref size, false, 2, 3, 0);
                if (result == 122) continue;
                if (result != 0) throw new Win32Exception((int)result);
                var rows = new List<(uint, int, int)>();
                for (var i = 0; i < Marshal.ReadInt32(buffer); i++)
                {
                    var row = IntPtr.Add(buffer, 4 + i * 24);
                    var port = (Marshal.ReadByte(row, 8) << 8) | Marshal.ReadByte(row, 9);
                    rows.Add((unchecked((uint)Marshal.ReadInt32(row, 4)), port, Marshal.ReadInt32(row, 20)));
                }
                return rows;
            } finally { Marshal.FreeHGlobal(buffer); }
        }
        throw new IOException("桥接连接状态正在变化，请稍后重试。");
    }

    [DllImport("iphlpapi.dll")]
    private static extern uint GetExtendedTcpTable(IntPtr table, ref int size, [MarshalAs(UnmanagedType.Bool)] bool order,
        int family, int tableClass, uint reserved);
}
