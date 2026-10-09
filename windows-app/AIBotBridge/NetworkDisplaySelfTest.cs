namespace AIBotBridge;

internal static class NetworkDisplaySelfTest
{
    internal static void Run()
    {
        var clock = new NetworkPublicationClock();
        (long Tick, bool Due)[] cadence = [(0, false), (250, false), (750, false), (1000, true),
            (1250, false), (1750, false), (2000, true), (5500, true), (5500, false), (5999, false), (6000, true)];
        foreach (var step in cadence)
            if (clock.Due(step.Tick) != step.Due)
                throw new InvalidOperationException("Network publication cadence drifted, accelerated or replayed a delayed tick.");
        NetworkSample[] raw = [new(0, 0), new(0, 0), new(0, 0), new(400000, 800000),
            new(0, 0), new(0, 0), new(0, 0), new(0, 0)];
        long[] expected = [0, 0, 0, 100000, 100000, 100000, 100000, 0];
        var live = new NetworkDisplayWindow();
        for (var i = 0; i < raw.Length; i++)
        {
            var header = live.Update(raw[i]);
            var endpoint = NetworkDisplayWindow.Smooth(raw.Take(i + 1))[^1];
            if (header.Upload != expected[i] || header.Download != expected[i] * 2 || header != endpoint)
                throw new InvalidOperationException("Network burst/stop numbers and curve endpoint diverged.");
        }
        var startup = new NetworkDisplayWindow();
        if (startup.Update(new(12345, 54321)) != new NetworkSample(12345, 54321))
            throw new InvalidOperationException("Network startup included unmeasured zero samples.");
        var ramp = NetworkDisplayWindow.Smooth(Enumerable.Range(1, 300).Select(i => new NetworkSample(i * 1000, i * 2000)));
        if (ramp[^1] != new NetworkSample(298500, 597000) || raw[3].Upload != 400000)
            throw new InvalidOperationException("Network smoothing changed measured data or lost the latest window.");
        Console.WriteLine("NETWORK_DISPLAY_OK one-second publication; delayed tick without replay; one-second mean; current endpoint; burst; stop; startup; ramp; raw history preserved");
    }

    internal static async Task RunLiveAsync()
    {
        var service = new SystemMetricsService();
        using var stop = new CancellationTokenSource();
        var sampling = service.RunAsync(stop.Token);
        var verified = 0;
        long sequence = 0;
        SystemMetricsSnapshot? previous = null;
        var deadline = Environment.TickCount64 + 8000;
        var intervals = new List<double>();
        try
        {
            while (verified < 5 && Environment.TickCount64 < deadline)
            {
                await Task.Delay(50);
                var snapshot = service.Snapshot;
                if (snapshot is null) continue;
                if (snapshot.SampleSequence == sequence)
                {
                    if (!ReferenceEquals(snapshot, previous))
                        throw new InvalidOperationException("Network numbers/history changed between one-second publications.");
                    continue;
                }
                if (previous is not null)
                {
                    var interval = (snapshot.UpdatedAt - previous.UpdatedAt).TotalMilliseconds;
                    if (interval < 500 || interval > 1750 || snapshot.SampleSequence - sequence < 2)
                        throw new InvalidOperationException("Live network publication cadence or raw sampling failed.");
                    intervals.Add(interval);
                }
                sequence = snapshot.SampleSequence;
                previous = snapshot;
                var measured = snapshot.History!.TakeLast(4).ToArray();
                if (snapshot.UploadBytesPerSecond != measured.Sum(x => x.Upload) / measured.Length ||
                    snapshot.DownloadBytesPerSecond != measured.Sum(x => x.Download) / measured.Length ||
                    snapshot.Samples![^1] != measured[^1])
                    throw new InvalidOperationException("Live Windows metrics header and latest raw window diverged.");
                verified++;
            }
            if (verified < 5) throw new InvalidOperationException("Live metrics sampling did not advance.");
            Console.WriteLine($"NETWORK_LIVE_OK {verified} one-second snapshots; intervals={string.Join(',', intervals.Select(x => x.ToString("0")))}ms; held numbers/history between updates; four-sample mean; raw wire tail retained");
        }
        finally
        {
            stop.Cancel();
            try { await sampling; } catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }
    }
}
