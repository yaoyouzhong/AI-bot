using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace AIBotBridge;

internal sealed class SystemMetricsService
{
    private readonly object _sync = new();
    private CpuTimes? _previousCpu;
    private readonly Dictionary<string, NetworkTotals> _previousNetwork = new();
    private NetworkInterface[] _adapters = [];
    private long _refreshAdaptersAt;
    private long? _previousTick;
    private int _sampling;
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
        if (Interlocked.Exchange(ref _sampling, 1) != 0) return;
        try { SampleCore(); }
        finally { Volatile.Write(ref _sampling, 0); }
    }

    private void SampleCore()
    {
        var now = DateTimeOffset.UtcNow;
        var tick = Environment.TickCount64;
        var seconds = _previousTick.HasValue ? (tick - _previousTick.Value) / 1000.0 : 0;
        var cpu = ReadCpuTimes();
        var network = ReadNetworkRates(tick, seconds);
        var memory = ReadMemoryPercent();
        lock (_sync)
        {
            if (_previousCpu.HasValue && _previousTick.HasValue)
            {
                _snapshot = new SystemMetricsSnapshot(
                    CpuPercent(_previousCpu.Value, cpu),
                    memory,
                    network.Sent,
                    network.Received,
                    now);
            }
            _previousCpu = cpu;
            _previousTick = tick;
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

    private NetworkTotals ReadNetworkRates(long tick, double seconds)
    {
        if (tick >= _refreshAdaptersAt)
        {
            _refreshAdaptersAt = tick + 30_000; // Includes failed enumeration: no busy retry loop.
            try { _adapters = NetworkInterface.GetAllNetworkInterfaces().Where(IsTrafficAdapter).ToArray(); }
            catch (NetworkInformationException) { /* Keep previous inventory until the next refresh. */ }
        }
        var current = new Dictionary<string, NetworkTotals>();
        foreach (var adapter in _adapters)
        {
            try
            {
                var stats = adapter.GetIPStatistics();
                current[adapter.Id] = new NetworkTotals(stats.BytesSent, stats.BytesReceived);
            }
            catch (NetworkInformationException)
            {
                // An adapter can disappear between enumeration and sampling.
            }
        }
        var rates = AggregateNetworkRates(_previousNetwork, current, seconds);
        _previousNetwork.Clear();
        foreach (var pair in current) _previousNetwork.Add(pair.Key, pair.Value);
        return rates;
    }

    internal static NetworkTotals AggregateNetworkRates(IReadOnlyDictionary<string, NetworkTotals> previous,
        IReadOnlyDictionary<string, NetworkTotals> current, double seconds)
    {
        long sent = 0, received = 0;
        foreach (var pair in current)
        {
            if (!previous.TryGetValue(pair.Key, out var baseline)) continue;
            sent += CalculateRate(baseline.Sent, pair.Value.Sent, seconds);
            received += CalculateRate(baseline.Received, pair.Value.Received, seconds);
        }
        return new NetworkTotals(sent, received);
    }

    private static bool IsTrafficAdapter(NetworkInterface adapter)
    {
        try
        {
            if (adapter.OperationalStatus != OperationalStatus.Up ||
                adapter.NetworkInterfaceType is not (NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211))
                return false;
            var excluded = new[] { "virtual", "vpn", "tap", "hyper-v", "vmware", "loopback", "wintun" };
            return !excluded.Any(word => adapter.Description.Contains(word, StringComparison.OrdinalIgnoreCase)) &&
                adapter.GetIPProperties().UnicastAddresses.Count > 0;
        }
        catch (NetworkInformationException) { return false; }
    }

    private static ulong ToUInt64(FileTime value) => ((ulong)value.High << 32) | value.Low;

    private readonly record struct CpuTimes(ulong Idle, ulong Kernel, ulong User);
    internal readonly record struct NetworkTotals(long Sent, long Received);

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
