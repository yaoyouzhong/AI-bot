using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace AIBotBridge;

internal static class ActivityCacheSelfTest
{
    internal static async Task RunAsync()
    {
        var empty = new Dictionary<string, LocalProviderUsage>();
        var idleObservation = new ActivitySample(new("offline", null), empty, Environment.TickCount64);
        await CheckClaudeAsync(idleObservation, "idle");
        await CheckClaudeAsync(idleObservation with { ClaudeIdleObservedAt = null }, "offline");
        await CheckClaudeAsync(idleObservation with { ClaudeIdleObservedAt = Environment.TickCount64 - 16000 }, "offline");
        var working = new Dictionary<string, LocalProviderUsage>
            { ["claude"] = new(42, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), "") };
        await CheckClaudeAsync(new(new("offline", null), working), "working");
        Console.WriteLine("CLAUDE_TERMINAL_ACTIVITY_OK blank idle session, exited or stale session, active work fallback");

        using var release = new ManualResetEventSlim();
        using var entered = new ManualResetEventSlim();
        var calls = 0;
        var sample = new ActivitySample(new("working", 0), new Dictionary<string, LocalProviderUsage>
            { ["codex"] = new(123, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), "") });
        var cache = new BackgroundActivityCache(() =>
        {
            if (Interlocked.Increment(ref calls) == 1) return sample;
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(20))) throw new TimeoutException("Test gate timed out.");
            throw new IOException("Synthetic scanner failure");
        });
        cache.Read();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!ReferenceEquals(cache.Read(), sample) && DateTime.UtcNow < deadline) await Task.Delay(10);
        if (!ReferenceEquals(cache.Read(), sample)) throw new Exception("Initial sample was not published.");
        await Task.Delay(5100);
        cache.Read();
        if (!entered.Wait(5000)) throw new Exception("Slow scan did not start.");
        var probe = new TcpListener(IPAddress.Loopback, 0);
        probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        using var stop = new CancellationTokenSource();
        var server = new LocalStatusServer(port);
        var running = server.RunAsync(() => SessionActivityReader.Capture(cache), stop.Token);
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(1) };
        var started = Environment.TickCount64;
        try
        {
            for (var i = 0; i < 10; i++)
            {
                var capture = Task.Run(() => SessionActivityReader.Capture(cache));
                var status = await capture.WaitAsync(TimeSpan.FromSeconds(1));
                if (status.Codex.TokensToday != 123 || DeviceStatusFrame.Create(status)["type"]!.GetValue<string>() != "status")
                    throw new Exception("Heartbeat lost cached data.");
                using var response = JsonDocument.Parse(await client.GetStringAsync($"http://127.0.0.1:{port}/status"));
                if (response.RootElement.GetProperty("codex").GetProperty("tokensToday").GetInt64() != 123)
                    throw new Exception("HTTP lost cached data.");
                await Task.Delay(1000);
            }
            if (calls != 2 || Environment.TickCount64 - started < 8000) throw new Exception("Scan overlap or insufficient stall duration.");
        }
        finally { release.Set(); stop.Cancel(); await running; }
        await Task.Delay(200);
        if (!ReferenceEquals(cache.Read(), sample)) throw new Exception("Failure overwrote the previous sample.");
        if (!JsonSerializer.Serialize(cache.Diagnostics()).Contains("IOException")) throw new Exception("Scanner failure was hidden.");
        Console.WriteLine("ACTIVITY_CACHE_OK 10s blocked scanner: HTTP/heartbeat responsive, no overlap, cache retained, failure reported");
    }

    private static async Task CheckClaudeAsync(ActivitySample sample, string expected)
    {
        var cache = new BackgroundActivityCache(() => sample);
        await cache.WaitForInitialScanAsync();
        var state = SessionActivityReader.Capture(cache).Claude.State;
        if (state != expected) throw new InvalidOperationException($"Expected Claude {expected}, got {state}.");
    }
}
