namespace AIBotBridge;

// Constant readouts with overlapping raw-sample tails. No user cache is written.
internal static class SystemRefreshDeviceTest
{
    internal static async Task RunAsync()
    {
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(35));
        long started=Environment.TickCount64;
        var session=Guid.NewGuid().ToString("N");
        int cpu=9;
        SystemMetricsSnapshot Metrics()
        {
            long seq=(Environment.TickCount64-started)/250+1;
            return new(Volatile.Read(ref cpu),55,100,200,DateTimeOffset.UtcNow) {
                SampleSession=session,SampleSequence=seq,
                Samples=Enumerable.Range(0,(int)Math.Min(12,seq)).Select(i=>new NetworkSample(100,200)).ToArray()};
        }
        StatusSnapshot Capture()=>new(1,"12:00",DateTimeOffset.UtcNow.ToUnixTimeSeconds(),28800,DateTimeOffset.UtcNow,new("idle",0),new("idle",0)){SystemMetrics=Metrics()};
        var serial=new SerialPublisher(null,BridgeSettings.Load().Get("serial_port"));
        var worker=serial.RunAsync(Capture,()=>[],stop.Token);
        var fast=serial.RunMetricsAsync(Metrics,stop.Token);
        UsbDeviceInfo? original=null;
        try {
            while(serial.PortName is null)await Task.Delay(200,stop.Token);
            original=serial.ReadDeviceInfo();
            if(!serial.SendDisplayMode("system"))throw new IOException("Cannot select system page.");
            await Task.Delay(4000,stop.Token);
            var before=serial.ReadDeviceInfo();
            await Task.Delay(5000,stop.Token);
            var after=serial.ReadDeviceInfo();
            uint Delta(string name)=>after.PageData!.Value.GetProperty(name).GetUInt32()-before.PageData!.Value.GetProperty(name).GetUInt32();
            uint frames=Delta("system_chart_frames"),samples=Delta("system_samples_consumed"),chrome=Delta("system_chrome_draws"),numbers=Delta("system_number_draws");
            if(!after.UsbActive||after.UptimeMs<=before.UptimeMs||frames is <16 or >24||samples is <16 or >24||chrome!=0||numbers!=0)
                throw new IOException($"Cadence mismatch: frames={frames},samples={samples},chrome={chrome},numbers={numbers}");
            Console.WriteLine($"SYSTEM_REFRESH_DEVICE_OK 5s frames={frames} samples={samples} static_chrome={chrome} unchanged_numbers={numbers}");
            Volatile.Write(ref cpu,100);await Task.Delay(1000,stop.Token);
            Volatile.Write(ref cpu,9);await Task.Delay(1000,stop.Token);
            var changed=serial.ReadDeviceInfo();
            uint replacements=changed.PageData!.Value.GetProperty("system_number_draws").GetUInt32()-after.PageData!.Value.GetProperty("system_number_draws").GetUInt32();
            if(replacements!=2)throw new IOException($"Expected exactly two CPU replacements, got {replacements}");
            Console.WriteLine("SYSTEM_NUMBER_CHANGE_OK 9->100->9 exactly two replacements");
        } finally {
            if(original is not null)serial.SendDisplayMode(original.Mode);
            stop.Cancel();
            try {await Task.WhenAll(worker,fast);}catch(OperationCanceledException)when(stop.IsCancellationRequested){}
        }
    }
}
