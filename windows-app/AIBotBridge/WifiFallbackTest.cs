namespace AIBotBridge;

internal interface IUsbFallbackDevice
{
    UsbDeviceInfo ReadDeviceInfo();
    void PauseTransmission();
    void ResumeTransmission();
}

internal static class WifiFallbackTest
{
    internal static async Task RunAsync(IUsbFallbackDevice device, CancellationToken cancellationToken,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        delay ??= Task.Delay;
        UsbDeviceInfo before;
        UsbDeviceInfo during;
        device.PauseTransmission();
        try
        {
            before = device.ReadDeviceInfo();
            if (!before.UsbActive) throw new IOException("测试前 USB 状态尚未建立，请稍后再试。");
            await delay(TimeSpan.FromSeconds(12), cancellationToken);
            during = device.ReadDeviceInfo(); // This request never renews the USB heartbeat.
        }
        finally { device.ResumeTransmission(); }

        await delay(TimeSpan.FromSeconds(3), cancellationToken);
        var recovered = device.ReadDeviceInfo();
        var fallback = !during.UsbActive && during.BridgeOnline &&
            during.UsbStatusCount == before.UsbStatusCount &&
            during.LanStatusCount > before.LanStatusCount && during.UptimeMs >= before.UptimeMs;
        var restored = recovered.UsbActive && recovered.UsbStatusCount > during.UsbStatusCount &&
            recovered.UptimeMs >= during.UptimeMs;
        if (!fallback || !restored)
            throw new IOException($"Wi-Fi 回退={(fallback ? "通过" : "未通过")}；USB 恢复={(restored ? "通过" : "未通过")}。" +
                $"\n前：{Describe(before)}\n回退：{Describe(during)}\n恢复：{Describe(recovered)}\n" +
                "已解除暂停；检查网络隔离、LAN 地址、防火墙及设备配网。此结果不自动判定固件故障。");
    }
    // Deliberately excludes SSID, pairing tokens, raw page data and addresses.
    private static string Describe(UsbDeviceInfo info) =>
        $"USB={info.UsbActive},在线={info.BridgeOnline},Wi-Fi地址已分配={!string.IsNullOrWhiteSpace(info.Ip)&&info.Ip!="0.0.0.0"},USB帧={info.UsbStatusCount},LAN帧={info.LanStatusCount},运行ms={info.UptimeMs}";

    internal static async Task RunHardwareAsync(bool fallback, bool stability = false)
    {
        using var shutdown = new CancellationTokenSource(stability
            ? TimeSpan.FromMinutes(7) : TimeSpan.FromSeconds(60));
        using var runtime = new BridgeRuntime(startRefresh: false);
        var settings = BridgeSettings.Load();
        var port = int.TryParse(Environment.GetEnvironmentVariable("AIBOT_HTTP_PORT"), out var value) &&
            value is > 0 and <= 65535 ? value : 8765;
        if (fallback && LanPairingFactory.FindPrivateAddress(null) is null)
            throw new IOException("没有可用的 LAN 地址，无法进行 Wi-Fi 回退测试。");
        var serial = new SerialPublisher(null, settings.Get("serial_port"));
        var lanTask = fallback
            ? LanBindingManager.RunAsync(port, serial, runtime.Capture, () => [], shutdown.Token)
            : Task.CompletedTask;
        var serialTask = serial.RunAsync(runtime.Capture, () => Array.Empty<ResourcePayload>(), shutdown.Token);
        try
        {
            while (serial.PortName is null)
            {
                if (lanTask.IsFaulted) await lanTask;
                if (serialTask.IsCompleted) await serialTask;
                await Task.Delay(250, shutdown.Token);
            }
            if (fallback)
            {
                while (serial.ConfiguredLanHost is null || serial.ConfiguredLanHost !=
                    LanPairingFactory.FindPrivateAddress(serial.DeviceHost)?.ToString())
                {
                    if (lanTask.IsFaulted) await lanTask;
                    if (serialTask.IsCompleted) await serialTask;
                    await Task.Delay(250, shutdown.Token);
                }
            }
            await Task.Delay(2500, shutdown.Token);
            if (lanTask.IsFaulted) await lanTask;
            if (stability) await RunStabilityAsync(serial, shutdown.Token);
            else if (fallback) await RunAsync(serial, shutdown.Token);
            else if (!serial.ReadDeviceInfo().UsbActive) throw new IOException("USB 状态未建立。");
            if (!stability)
                Console.WriteLine(fallback ? "WIFI_FALLBACK_TEST_OK" : "USB_MANAGEMENT_TEST_OK");
        }
        finally
        {
            serial.ResumeTransmission();
            shutdown.Cancel();
            try { await Task.WhenAll(serialTask, lanTask); }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
        }
    }

    private static async Task RunStabilityAsync(SerialPublisher serial, CancellationToken cancellationToken)
    {
        var before = serial.ReadDeviceInfo();
        if (!before.UsbActive) throw new IOException("测试前 USB 状态尚未建立。");
        var previous = before;
        serial.PauseTransmission();
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(12), cancellationToken);
            for (var sample = 0; sample <= 30; sample++)
            {
                serial.PauseTransmission(); // Renew the 30-second safety lease while the test owns the port.
                var current = serial.ReadDeviceInfo(); // Diagnostic requests do not renew USB status.
                if (current.UsbActive || !current.BridgeOnline ||
                    current.UsbStatusCount != before.UsbStatusCount ||
                    current.LanStatusCount <= previous.LanStatusCount ||
                    current.UptimeMs < previous.UptimeMs || IsCachedOrOffline(current))
                    throw new IOException($"Wi-Fi 链路在第 {sample * 10 + 12} 秒未持续更新：" +
                        $"USB={current.UsbActive}, 在线={current.BridgeOnline}, " +
                        $"USB帧={current.UsbStatusCount}, LAN帧={current.LanStatusCount}, " +
                        $"上次LAN帧={previous.LanStatusCount}。");
                previous = current;
                if (sample % 6 == 0)
                    Console.WriteLine($"WIFI_STABILITY_PROGRESS seconds={sample * 10 + 12} lan_count={current.LanStatusCount} usb_count={current.UsbStatusCount}");
                if (sample < 30) await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
        }
        finally { serial.ResumeTransmission(); }

        await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
        var restored = serial.ReadDeviceInfo();
        if (!restored.UsbActive || restored.UsbStatusCount <= previous.UsbStatusCount)
            throw new IOException("Wi-Fi 持续测试完成，但 USB 自动恢复未通过。");
        Console.WriteLine($"WIFI_STABILITY_5M_OK lan_delta={previous.LanStatusCount - before.LanStatusCount} " +
            $"usb_delta_during=0 usb_recovered=true");
    }

    private static bool IsCachedOrOffline(UsbDeviceInfo info)
    {
        if (info.PageData is not { ValueKind: System.Text.Json.JsonValueKind.Object } page)
            return true;
        return !page.TryGetProperty("display_cached", out var cached) || cached.GetBoolean() ||
            !page.TryGetProperty("rendered_page", out var rendered) ||
            rendered.GetString() == "offline";
    }
}
