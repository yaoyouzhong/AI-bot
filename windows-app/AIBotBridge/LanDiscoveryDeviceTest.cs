namespace AIBotBridge;

internal static class LanDiscoveryDeviceTest
{
    internal static async Task RunAsync()
    {
        var normalPort = int.TryParse(Environment.GetEnvironmentVariable("AIBOT_HTTP_PORT"), out var value) &&
            value is > 0 and <= 65535 ? value : 8765;
        var testPort = normalPort == 18767 ? 18768 : 18767;
        var normal = LanPairingFactory.Create(normalPort)
            ?? throw new IOException("No private LAN address is available.");
        var test = normal with { Port = testPort };
        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        using var runtime = new BridgeRuntime(startRefresh: false);
        var serial = new SerialPublisher(normal, BridgeSettings.Load().Get("serial_port"));
        var discovery = new LanDiscoveryServer();
        discovery.SetBinding(test);
        var lanTask = new LanStatusServer(test).RunAsync(runtime.Capture, shutdown.Token);
        var discoveryTask = discovery.RunAsync(shutdown.Token);
        var serialTask = serial.RunAsync(runtime.Capture, () => [], shutdown.Token);
        var succeeded = false;
        var restored = false;
        try
        {
            while (serial.ConfiguredLanPort != normalPort || serial.PortName is null)
            {
                if (serialTask.IsCompleted) await serialTask;
                if (lanTask.IsFaulted) await lanTask;
                if (discoveryTask.IsFaulted) await discoveryTask;
                await Task.Delay(250, shutdown.Token);
            }
            await Task.Delay(2500, shutdown.Token);
            var before = serial.ReadDeviceInfo();
            if (!before.UsbActive) throw new IOException("USB baseline is not fresh.");
            serial.PauseTransmission();
            for (var sample = 1; sample <= 8; sample++)
            {
                await Task.Delay(TimeSpan.FromSeconds(5), shutdown.Token);
                serial.PauseTransmission();
                var now = serial.ReadDeviceInfo();
                if (now.UsbStatusCount != before.UsbStatusCount)
                    throw new IOException("USB status changed during LAN discovery test.");
                if (!now.UsbActive && now.BridgeOnline && now.LanStatusCount > before.LanStatusCount)
                {
                    succeeded = true;
                    Console.WriteLine($"LAN_DISCOVERY_DEVICE_OK elapsed_seconds={sample * 5} " +
                        $"lan_delta={now.LanStatusCount - before.LanStatusCount} " +
                        $"request_seen={discovery.DeviceHost is not null}");
                    break;
                }
            }
            if (!succeeded) throw new IOException("Device did not recover through authenticated LAN discovery within 40 seconds.");
        }
        finally
        {
            serial.ResumeTransmission();
            // USB remains connected for this test. Restore the normal port even if discovery fails.
            serial.SetPairing(null);
            try
            {
                using var restore = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                while (serial.ConfiguredLanPort != 0 && !restore.IsCancellationRequested)
                    await Task.Delay(100, restore.Token);
                serial.SetPairing(normal);
                while (serial.ConfiguredLanPort != normalPort && !restore.IsCancellationRequested)
                    await Task.Delay(100, restore.Token);
                restored = serial.ConfiguredLanPort == normalPort;
            }
            catch (OperationCanceledException) { }
            shutdown.Cancel();
            try { await Task.WhenAll(serialTask, lanTask, discoveryTask); }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
        }
        if (!restored) throw new IOException("Normal LAN pairing was not restored over USB.");
    }
}
