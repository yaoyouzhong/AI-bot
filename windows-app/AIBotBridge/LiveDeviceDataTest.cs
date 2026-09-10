namespace AIBotBridge;

// Exercises the production runtime and resource worker, not acceptance fixtures.
internal static class LiveDeviceDataTest
{
    internal static async Task RunAsync()
    {
        using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        using var runtime = new BridgeRuntime();
        var serial = new SerialPublisher(null, BridgeSettings.Load().Get("serial_port"));
        // One resource sender: avoid testing each asset twice and racing a failed
        // explicit transfer against the production worker's next retry.
        var worker = serial.RunAsync(runtime.Capture, () => [], shutdown.Token);
        try
        {
            while (true)
            {
                await Task.Delay(3000, shutdown.Token);
                if (worker.IsCompleted) await worker;
                if (serial.PortName is null) continue;
                var host = runtime.Capture();
                var device = serial.ReadDeviceInfo();
                if (device.PageData is not { } pages)
                    throw new IOException("Firmware has no page-data diagnostics.");
                if (!device.UsbActive || host.Weather is null || host.Stocks is null ||
                    host.SystemMetrics is null || !pages.GetProperty("weather").GetBoolean() ||
                    !pages.GetProperty("system").GetBoolean() ||
                    pages.GetProperty("stock_count").GetInt32() != Math.Min(20, host.Stocks.Quotes.Count) ||
                    Math.Abs(pages.GetProperty("temperature").GetDouble() - host.Weather.Temperature) > 0.01)
                    continue;
                if (host.Quotas?.Codex is not null && !pages.GetProperty("codex").GetBoolean()) continue;
                // Page-data readback alone says nothing about the original pet pixels.
                var artwork=runtime.Resources().ToArray();
                foreach(var kind in new[]{BinaryResourceKind.ClaudePetAnimation,BinaryResourceKind.CodexPetAnimation,BinaryResourceKind.ClaudeLogo,BinaryResourceKind.CodexLogo})
                {
                    var resource=artwork.SingleOrDefault(r=>r.Kind==kind);
                    if(resource is null) throw new IOException($"Missing required local legacy artwork: {kind}. Import it before visual acceptance.");
                    if(!serial.SendResource(resource.Kind,resource.Data)) throw new IOException($"Original artwork transfer failed: {kind}. {serial.LastResourceFailure}");
                    Console.WriteLine($"LEGACY_ARTWORK_ACK_OK kind={kind} bytes={resource.Data.Length}");
                }
                foreach (var resource in artwork.Where(r => r.Kind is not (BinaryResourceKind.ClaudePetAnimation or BinaryResourceKind.CodexPetAnimation or BinaryResourceKind.ClaudeLogo or BinaryResourceKind.CodexLogo)))
                {
                    if (!serial.SendResource(resource.Kind, resource.Data)) throw new IOException(serial.LastResourceFailure);
                    Console.WriteLine($"LIVE_RESOURCE_ACK_OK kind={resource.Kind} bytes={resource.Data.Length}");
                }
                Console.WriteLine($"LIVE_DEVICE_DATA_OK port={serial.PortName} usb_count={device.UsbStatusCount} " +
                    $"page_data={pages.GetRawText()}");
                Console.WriteLine("Unavailable providers/music still require their own account/session validation.");
                return;
            }
        }
        finally
        {
            shutdown.Cancel();
            try { await worker; }
            catch (OperationCanceledException) when (shutdown.IsCancellationRequested) { }
        }
    }
}
