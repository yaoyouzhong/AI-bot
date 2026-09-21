using System.Diagnostics;

namespace AIBotBridge;

// Run only after the normal bridge has exited; restart it afterwards so its
// saved policy and resources replace this bounded USB acceptance session.
internal static class StockDisplayDeviceTest
{
    internal static async Task RunAsync()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var settings = BridgeSettings.Load();
        var stocks = new StockService(settings, false);
        await stocks.RefreshAsync(stop.Token);
        if (stocks.Snapshot is not { Stale: false } quotes) throw new IOException("Live stock refresh failed.");
        string mode = "weather";
        int alert = 0;
        StatusSnapshot Capture() => new(1, "12:00", DateTimeOffset.UtcNow.ToUnixTimeSeconds(), 28800, DateTimeOffset.UtcNow,
            new("idle", 0, CompletionActive: Volatile.Read(ref alert) == 0, NeedsInput: Volatile.Read(ref alert) == 1), new("idle", 0), Stocks: quotes,
            DomesticActivity: new("kimi", "idle", Volatile.Read(ref alert) == 2, new Dictionary<string,LocalProviderUsage>()),
            DisplayPolicy: new(Volatile.Read(ref mode), false, 15, ["weather", "stocks"]));
        var serial = new SerialPublisher(null, settings.Get("serial_port"));
        var writes = new System.Collections.Concurrent.ConcurrentDictionary<string, long>();
        serial.DisplayModeWritten += value => writes[value] = Environment.TickCount64;
        var worker = serial.RunAsync(Capture, () => [], stop.Token);
        try
        {
            while (serial.PortName is null) await Task.Delay(100, stop.Token);
            var initial = serial.ReadDeviceInfo();
            // Transfer the actual user's current labels, not synthetic device content.
            var labels = LocalizedTextResources.RenderStockNames(quotes.Quotes.Select(q => q.Name).ToArray());
            var upload = Task.Run(() => serial.SendResource(BinaryResourceKind.StockLabels, labels));
            await Task.Delay(200, stop.Token);
            Require(!upload.IsCompleted, "Resource transfer must still be running for contention test");
            Volatile.Write(ref mode, "stocks");
            long requestedAt = Environment.TickCount64;
            var enqueue = Stopwatch.StartNew();
            Require(serial.SendDisplayMode("stocks"), "USB display command accepted");
            enqueue.Stop();
            Require(enqueue.ElapsedMilliseconds < 250, "Display submission blocked UI");
            while (!writes.ContainsKey("stocks")) await Task.Delay(5, stop.Token);
            long latency = writes["stocks"] - requestedAt;
            Require(!upload.IsCompleted, "Display must be written before the full resource completes");
            Require(latency < 1000, "Healthy USB display delivery exceeded one second");
            Require(await upload, "Resource ACK/CRC transfer must still succeed");
            var device = serial.ReadDeviceInfo();
            Require(device.PageData?.GetProperty("rendered_page").GetString() == "stocks" &&
                device.PageData?.GetProperty("effective_mode").GetString() == "stocks" &&
                device.PageData?.GetProperty("stock_draws").GetUInt32() > 0,
                "Stock drawing must execute despite active completion notification; requested mode alone is insufficient");
            Require(device.Mode == "stocks" && device.UsbActive && device.BridgeOnline && device.UptimeMs > initial.UptimeMs,
                "Device confirms requested mode and healthy USB");
            Console.WriteLine($"STOCK_DISPLAY_DEVICE_OK submit_ms={enqueue.ElapsedMilliseconds} delivery_ms={latency} during_transfer=true mode={device.Mode} stocks={quotes.Quotes.Count}");
            foreach (var next in new[] { "weather", "system", "stocks" })
            {
                writes.TryRemove(next, out _);
                Volatile.Write(ref mode, next);
                requestedAt = Environment.TickCount64;
                Require(serial.SendDisplayMode(next), "Idle USB command accepted");
                while (!writes.ContainsKey(next)) await Task.Delay(5, stop.Token);
                UsbDeviceInfo shown;
                do
                {
                    await Task.Delay(30, stop.Token);
                    shown = await Task.Run(serial.ReadDeviceInfo, stop.Token);
                } while (shown.PageData?.GetProperty("rendered_page").GetString() != next && Environment.TickCount64 - requestedAt < 1500);
                Require(shown.Mode == next && shown.PageData?.GetProperty("rendered_page").GetString() == next,
                    "Device must render the latest manual selection despite completion notification");
                Console.WriteLine($"DISPLAY_DEVICE_CONFIRM mode={next} delivery_ms={writes[next] - requestedAt} rendered_confirm_ms={Environment.TickCount64 - requestedAt}");
            }
            await Task.Delay(2200, stop.Token);
            Require(serial.ReadDeviceInfo().Mode == "stocks", "Heartbeat must not undo latest mode");
            for (int scenario = 0; scenario < 3; scenario++)
            {
                Volatile.Write(ref alert, scenario);
                await Task.Delay(2300, stop.Token);
                var shown = serial.ReadDeviceInfo();
                Require(shown.PageData?.GetProperty("rendered_page").GetString() == "stocks", "Alert heartbeat stole manual stock page");
                Console.WriteLine($"MANUAL_ALERT_DEVICE_OK alert={scenario} rendered=stocks draws={shown.PageData?.GetProperty("stock_draws").GetUInt32()}");
            }
            Volatile.Write(ref alert, 0);
            Volatile.Write(ref mode, "auto");
            serial.SendDisplayMode("auto");
            await Task.Delay(2300, stop.Token);
            Require(serial.ReadDeviceInfo().PageData?.GetProperty("rendered_page").GetString() == "codex", "Auto mode must retain completion navigation");
            Console.WriteLine("AUTO_ALERT_DEVICE_OK completion rendered=codex");
        }
        finally
        {
            stop.Cancel();
            try { await worker; } catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new IOException(message); }
}
