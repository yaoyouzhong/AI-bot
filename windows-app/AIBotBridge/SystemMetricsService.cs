using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace AIBotBridge;

internal sealed class SystemMetricsService
{
    private readonly object _sync = new();
    private CpuTimes? _previousCpu;
    private NetworkTotals? _previousNetwork;
    private DateTimeOffset? _previousAt;
    private SystemMetricsSnapshot? _snapshot;

    internal SystemMetricsSnapshot? Snapshot
    {
        get { lock (_sync) return _snapshot; }
    }

    internal async Task RunAsync(CancellationToken cancellationToken)
    {
        Sample();
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(cancellationToken)) Sample();
    }

    internal async Task<SystemMetricsSnapshot> CaptureForSelfTestAsync()
    {
        Sample();
        await Task.Delay(1100);
        Sample();
        return Snapshot ?? throw new InvalidOperationException("System metrics did not produce a second sample.");
    }

    private void Sample()
    {
        var now = DateTimeOffset.UtcNow;
        var cpu = ReadCpuTimes();
        var network = ReadNetworkTotals();
        var memory = ReadMemoryPercent();
        lock (_sync)
        {
            if (_previousCpu.HasValue && _previousNetwork.HasValue && _previousAt.HasValue)
            {
                var seconds = Math.Max(0.001, (now - _previousAt.Value).TotalSeconds);
                _snapshot = new SystemMetricsSnapshot(
                    CpuPercent(_previousCpu.Value, cpu),
                    memory,
                    CalculateRate(_previousNetwork.Value.Sent, network.Sent, seconds),
                    CalculateRate(_previousNetwork.Value.Received, network.Received, seconds),
                    now);
            }
            _previousCpu = cpu;
            _previousNetwork = network;
            _previousAt = now;
        }
    }

    internal static long CalculateRate(long previous, long current, double elapsedSeconds) =>
        current >= previous && elapsedSeconds > 0
            ? (long)Math.Round((current - previous) / elapsedSeconds) : 0;

    private static double CpuPercent(CpuTimes previous, CpuTimes current)
    {
        var idle = current.Idle - previous.Idle;
        var total = current.Kernel - previous.Kernel + current.User - previous.User;
        return total > 0 ? Math.Clamp(100.0 * (total - idle) / total, 0, 100) : 0;
    }

    private static CpuTimes ReadCpuTimes()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return default;
        return new CpuTimes(ToUInt64(idle), ToUInt64(kernel), ToUInt64(user));
    }

    private static double ReadMemoryPercent()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        return GlobalMemoryStatusEx(ref status) ? status.MemoryLoad : 0;
    }

    private static NetworkTotals ReadNetworkTotals()
    {
        long sent = 0, received = 0;
        foreach (var adapter in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (adapter.OperationalStatus != OperationalStatus.Up ||
                adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            try
            {
                var stats = adapter.GetIPv4Statistics();
                sent += stats.BytesSent;
                received += stats.BytesReceived;
            }
            catch (NetworkInformationException)
            {
                // An adapter can disappear between enumeration and sampling.
            }
        }
        return new NetworkTotals(sent, received);
    }

    private static ulong ToUInt64(FileTime value) => ((ulong)value.High << 32) | value.Low;

    private readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);
    private readonly record struct NetworkTotals(long Sent, long Received);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTime
    {
        internal uint Low;
        internal uint High;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatus
    {
        internal uint Length;
        internal uint MemoryLoad;
        internal ulong TotalPhysical;
        internal ulong AvailablePhysical;
        internal ulong TotalPageFile;
        internal ulong AvailablePageFile;
        internal ulong TotalVirtual;
        internal ulong AvailableVirtual;
        internal ulong AvailableExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);
}
