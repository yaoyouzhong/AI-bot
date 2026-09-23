using System.Text.Json;

namespace AIBotBridge;

// Real USB test: a diagnostic request never renews status freshness.
internal static class BridgeGraceDeviceTest
{
    internal static async Task RunAsync()
    {
        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(65));
        var serial = new SerialPublisher(null, BridgeSettings.Load().Get("serial_port"));
        var worker = serial.RunAsync(Capture, () => [], stop.Token);
        var paused = false;
        try
        {
            while (serial.PortName is null)
            {
                if (worker.IsCompleted) await worker;
                await Task.Delay(200, stop.Token);
            }
            await Task.Delay(2500, stop.Token);
            var before = serial.ReadDeviceInfo();
            if (!before.UsbActive || !before.BridgeOnline)
                throw new IOException("USB status was not established before the pause.");

            serial.PauseTransmission();
            paused = true;
            await Task.Delay(TimeSpan.FromSeconds(12), stop.Token);
            var cached = serial.ReadDeviceInfo();
            if (cached.UsbActive || cached.BridgeOnline || !DisplayCached(cached) ||
                RenderedPage(cached) == "offline" || cached.UsbStatusCount != before.UsbStatusCount)
                throw new IOException("The 12-second gap did not retain the cached page with offline transport diagnostics.");
            Console.WriteLine("BRIDGE_GRACE_12S_OK USB stale, cached page visible, no new status");

            // The transport pause has a 30-second safety limit; extend this
            // controlled test so it can observe the 30-second display boundary.
            serial.PauseTransmission();
            await Task.Delay(TimeSpan.FromSeconds(21), stop.Token);
            var offline = serial.ReadDeviceInfo();
            if (offline.UsbActive || offline.BridgeOnline || DisplayCached(offline) ||
                RenderedPage(offline) != "offline" || offline.UsbStatusCount != before.UsbStatusCount)
                throw new IOException("The 33-second gap did not enter the offline page.");
            Console.WriteLine("BRIDGE_GRACE_33S_OK offline page shown");

            serial.ResumeTransmission();
            paused = false;
            await Task.Delay(TimeSpan.FromSeconds(3), stop.Token);
            var recovered = serial.ReadDeviceInfo();
            if (!recovered.UsbActive || !recovered.BridgeOnline || DisplayCached(recovered) ||
                RenderedPage(recovered) == "offline" || recovered.UsbStatusCount <= before.UsbStatusCount)
                throw new IOException("USB status did not recover after the pause.");
            Console.WriteLine("BRIDGE_GRACE_RECOVERY_OK USB and page restored");
        }
        finally
        {
            if (paused) serial.ResumeTransmission();
            stop.Cancel();
            try { await worker; }
            catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        }
    }

    private static StatusSnapshot Capture()
    {
        var now = DateTimeOffset.Now;
        return new(1, now.ToString("HH:mm:ss"), now.ToUnixTimeSeconds(), (int)now.Offset.TotalSeconds,
            now, new("idle", 0), new("idle", 0));
    }

    private static bool DisplayCached(UsbDeviceInfo info) =>
        PageData(info).GetProperty("display_cached").GetBoolean();

    private static string? RenderedPage(UsbDeviceInfo info) =>
        PageData(info).GetProperty("rendered_page").GetString();

    private static JsonElement PageData(UsbDeviceInfo info) =>
        info.PageData ?? throw new IOException("Device page diagnostics are missing.");
}
