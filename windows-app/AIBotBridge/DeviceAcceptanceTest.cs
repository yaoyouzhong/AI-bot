namespace AIBotBridge;

// Synthetic display values over a real USB link. Never claims live-account or optical verification.
internal static class DeviceAcceptanceTest
{
    internal static async Task RunAsync()
    {
        using var shutdown = new CancellationTokenSource(TimeSpan.FromMinutes(4));
        var serial = new SerialPublisher(null, BridgeSettings.Load().Get("serial_port"));
        var worker = serial.RunAsync(Fixture, () => Array.Empty<ResourcePayload>(), shutdown.Token);
        UsbDeviceInfo? original = null;
        try
        {
            while (serial.PortName is null)
            {
                if (worker.IsCompleted) await worker;
                await Task.Delay(250, shutdown.Token);
            }
            await Task.Delay(2500, shutdown.Token);
            original = serial.ReadDeviceInfo();
            Console.WriteLine($"DEVICE_CONNECTED port={serial.PortName} uptime={original.UptimeMs} usb={original.UsbActive}");
            if (!original.UsbActive) throw new IOException("Initial USB state is not fresh.");

            var resources = new[]
            {
                (BinaryResourceKind.WeatherText, LocalizedTextResources.RenderLines(232, 24, ["测试城市  多云"], 20, 24)),
                (BinaryResourceKind.StockLabels, LocalizedTextResources.RenderLines(120, 400, ["上证指数", "腾讯控股", "Apple", "测试股票"], 18, 20)),
                (BinaryResourceKind.TextBitmap, NowPlayingService.RenderTextBitmap("真机验收测试", "合成数据")),
                (BinaryResourceKind.MusicCover, TestCover())
            };
            var failedResources = new List<string>();
            foreach (var (kind, bytes) in resources)
            {
                var accepted = serial.SendResource(kind, bytes);
                if (!accepted) failedResources.Add(kind.ToString());
                Console.WriteLine($"RESOURCE_ACK_{(accepted ? "OK" : "FAILED")} kind={kind} bytes={bytes.Length}");
            }

            foreach (var mode in new[] { "dual", "weather", "stocks", "quotas", "domestic", "system", "music", "pet", "screensaver" })
            {
                if (!serial.SendDisplayMode(mode)) throw new IOException($"Could not send mode {mode}.");
                await Task.Delay(mode is "pet" or "quotas" ? 14000 : 5000, shutdown.Token);
                var info = serial.ReadDeviceInfo();
                if (info.Mode != mode || !info.UsbActive || info.UptimeMs < original.UptimeMs)
                    throw new IOException($"Mode or USB continuity failed: {mode}.");
                Console.WriteLine($"MODE_ACK_OK mode={mode} usb_count={info.UsbStatusCount} uptime={info.UptimeMs}");
            }
            foreach (var brightness in new[] { 30, 75 })
            {
                if (!serial.SendBrightness(brightness)) throw new IOException("Brightness send failed.");
                await Task.Delay(500, shutdown.Token);
                if (serial.ReadDeviceInfo().Brightness != brightness) throw new IOException("Brightness readback failed.");
            }
            Console.WriteLine("BRIGHTNESS_READBACK_OK");
            serial.PauseTransmission();
            try
            {
                await Task.Delay(10000, shutdown.Token);
                var offline = serial.ReadDeviceInfo();
                if (offline.UsbActive) throw new IOException("USB expiry failed.");
                Console.WriteLine($"USB_EXPIRY_OK bridge_online={offline.BridgeOnline}");
            }
            finally { serial.ResumeTransmission(); }
            await Task.Delay(3000, shutdown.Token);
            if (!serial.ReadDeviceInfo().UsbActive) throw new IOException("USB did not recover.");
            if (failedResources.Count > 0) throw new IOException("Resource failures: " + string.Join(", ", failedResources));
            Console.WriteLine("DEVICE_ACCEPTANCE_USB_OK (synthetic values; visual confirmation still required; pet asset not replaced)");
        }
        finally
        {
            serial.ResumeTransmission();
            if (original is not null)
            {
                var modeRestored = serial.SendDisplayMode(original.Mode);
                var brightnessRestored = serial.SendBrightness(original.Brightness);
                Console.WriteLine($"RESTORE_SENT mode={modeRestored} brightness={brightnessRestored}");
                if (modeRestored && brightnessRestored)
                {
                    await Task.Delay(300);
                    var restored = serial.ReadDeviceInfo();
                    if (restored.Mode != original.Mode || restored.Brightness != original.Brightness)
                        throw new IOException("Original mode/brightness did not restore.");
                    Console.WriteLine("RESTORE_READBACK_OK");
                }
            }
            shutdown.Cancel();
            try { await worker; }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
        }
    }

    private static byte[] TestCover()
    {
        using var bitmap = new Bitmap(112, 112);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.DarkBlue);
        graphics.FillRectangle(Brushes.Cyan, 12, 12, 88, 88);
        graphics.FillRectangle(Brushes.DarkBlue, 32, 32, 48, 48);
        return PetAssetImporter.EncodeRgb565(bitmap);
    }

    private static StatusSnapshot Fixture()
    {
        var now = DateTimeOffset.Now;
        var quota = new ProviderQuotaSnapshot("codex", "TEST", 18.5, now.AddHours(2), 42,
            now.AddDays(3), 5, Enumerable.Range(1, 5).Select(day => now.AddDays(day).ToUnixTimeSeconds()).ToArray(), now, false);
        var domestic = new DomesticProviderQuotaSnapshot("deepseek", "TEST", 20, now.AddHours(1), 35,
            now.AddDays(2), 77.88, 22.12, "CNY", now, false);
        return new StatusSnapshot(1, now.ToString("HH:mm:ss"), now.ToUnixTimeSeconds(),
            (int)now.Offset.TotalSeconds, now, new ToolState("working", 1), new ToolState("idle", 20),
            Weather: new WeatherSnapshot("测试城市", "多云", 26, 31, 19, 58, 2, 13.2, 46, "TEST", now, false),
            Stocks: new StockSnapshot(new[]
            {
                new StockQuote("sh000001", "000001", "上证指数", "3821.44", "+0.85%", 1),
                new StockQuote("hk00700", "00700", "腾讯控股", "612.50", "-1.20%", -1),
                new StockQuote("usAAPL", "AAPL", "Apple", "238.10", "+0.14%", 1),
                new StockQuote("sz000001", "000001", "测试股票", "10.00", "0.00%", 0)
            }, now, false),
            Quotas: new QuotaSnapshot(quota with { Provider = "claude", ResetCreditsAvailable = null, ResetCreditExpiresAt = [] }, quota),
            DomesticQuotas: new DomesticQuotaSnapshot(domestic with { Provider = "alibaba", Balance = null },
                domestic with { Provider = "kimi", Balance = null }, domestic with { Provider = "minimax", Balance = null }, domestic),
            SystemMetrics: new SystemMetricsSnapshot(31.4, 72.8, 238900, 4821100, now),
            Music: new MusicSnapshot("真机验收测试", "合成数据", "TEST", true, 95, 260, now));
    }
}
