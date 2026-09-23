namespace AIBotBridge;

internal sealed record ActivitySample(ToolState Codex,
    IReadOnlyDictionary<string, LocalProviderUsage> Usage);

// Readers never acquire the scanner's locks or perform filesystem I/O.
internal sealed class BackgroundActivityCache(Func<ActivitySample> scan)
{
    private ActivitySample _sample = new(new("offline", null), new Dictionary<string, LocalProviderUsage>());
    private long _nextScan;
    private int _scanning;
    private long _lastDurationMs;
    private string? _lastError;
    private readonly TaskCompletionSource _initialized = new(TaskCreationOptions.RunContinuationsAsynchronously);

    internal async Task WaitForInitialScanAsync()
    {
        Read();
        await _initialized.Task.WaitAsync(TimeSpan.FromSeconds(30));
    }

    internal ActivitySample Read()
    {
        if (Environment.TickCount64 >= Volatile.Read(ref _nextScan) &&
            Interlocked.CompareExchange(ref _scanning, 1, 0) == 0)
            _ = Task.Run(Refresh);
        return Volatile.Read(ref _sample);
    }

    private void Refresh()
    {
        var started = Environment.TickCount64;
        try
        {
            Volatile.Write(ref _sample, scan());
            Volatile.Write(ref _lastError, null);
        }
        catch (Exception ex)
        {
            // Keep the previous sample and expose failure without logging user data.
            Volatile.Write(ref _lastError, ex.GetType().Name);
        }
        finally
        {
            Interlocked.Exchange(ref _lastDurationMs, Environment.TickCount64 - started);
            // A scan enumerates every session file. Leave room for the serial
            // publisher when the host is busy; activity states have 90s/15m thresholds.
            Volatile.Write(ref _nextScan, Environment.TickCount64 + 5000);
            Volatile.Write(ref _scanning, 0);
            _initialized.TrySetResult();
        }
    }

    internal object Diagnostics() => new
    {
        scanning = Volatile.Read(ref _scanning) != 0,
        lastDurationMs = Interlocked.Read(ref _lastDurationMs),
        lastError = Volatile.Read(ref _lastError)
    };
}
