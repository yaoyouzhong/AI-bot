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

    internal static async Task RunHardwareAsync(bool fallback)
    {
        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        using var runtime = new BridgeRuntime(startRefresh: false);
        var settings = BridgeSettings.Load();
        var port = int.TryParse(Environment.GetEnvironmentVariable("AIBOT_HTTP_PORT"), out var value) &&
            value is > 0 and <= 65535 ? value : 8765;
        var pairing = fallback ? LanPairingFactory.Create(port) : null;
        if (fallback && pairing is null) throw new IOException("没有可用的 LAN 地址，无法进行 Wi-Fi 回退测试。");
        var serial = new SerialPublisher(pairing, settings.Get("serial_port"));
        var lanTask = pairing is null ? Task.CompletedTask :
            new LanStatusServer(pairing).RunAsync(runtime.Capture, shutdown.Token);
        var serialTask = serial.RunAsync(runtime.Capture, () => Array.Empty<ResourcePayload>(), shutdown.Token);
        try
        {
            while (serial.PortName is null)
            {
                if (lanTask.IsFaulted) await lanTask;
                if (serialTask.IsCompleted) await serialTask;
                await Task.Delay(250, shutdown.Token);
            }
            await Task.Delay(2500, shutdown.Token);
            if (lanTask.IsFaulted) await lanTask;
            if (fallback) await RunAsync(serial, shutdown.Token);
            else if (!serial.ReadDeviceInfo().UsbActive) throw new IOException("USB 状态未建立。");
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
}
