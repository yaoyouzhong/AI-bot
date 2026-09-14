using System.Net;
using System.Net.Sockets;
using System.Text.Json;

namespace AIBotBridge;

internal static class ActivityCacheSelfTest
{
    internal static async Task RunAsync()
    {
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
        await Task.Delay(1100);
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
}
