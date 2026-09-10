namespace AIBotBridge;

internal static class ActivityRefreshDeviceTest
{
    internal static async Task RunAsync()
    {
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(35));
        int tokens=123;
        StatusSnapshot Capture()=>new(1,"12:00",DateTimeOffset.UtcNow.ToUnixTimeSeconds(),28800,DateTimeOffset.UtcNow,
            new("idle",0){TokensToday=Volatile.Read(ref tokens)},new("idle",0){TokensToday=456});
        var serial=new SerialPublisher(null,BridgeSettings.Load().Get("serial_port"));
        var worker=serial.RunAsync(Capture,()=>[],stop.Token);
        UsbDeviceInfo? original=null;
        try {
            while(serial.PortName is null)await Task.Delay(200,stop.Token);
            original=serial.ReadDeviceInfo();
            if(!serial.SendDisplayMode("activity"))throw new IOException("Cannot select activity page.");
            await Task.Delay(2500,stop.Token);
            var before=serial.ReadDeviceInfo();
            await Task.Delay(5000,stop.Token);
            var after=serial.ReadDeviceInfo();
            uint Delta(string name)=>after.PageData!.Value.GetProperty(name).GetUInt32()-before.PageData!.Value.GetProperty(name).GetUInt32();
            uint clock=Delta("activity_clock_draws"),chrome=Delta("activity_chrome_draws"),data=Delta("activity_data_draws");
            if(!after.UsbActive||after.UptimeMs<=before.UptimeMs||clock is <4 or >6||chrome!=0||data!=0)
                throw new IOException($"Unexpected refresh: clock={clock},chrome={chrome},data={data}");
            Console.WriteLine($"ACTIVITY_REFRESH_OK 5s clock={clock} chrome={chrome} unchanged_data={data}");
            Volatile.Write(ref tokens,9);await Task.Delay(3000,stop.Token);
            var changed=serial.ReadDeviceInfo();
            uint replacements=changed.PageData!.Value.GetProperty("activity_data_draws").GetUInt32()-after.PageData!.Value.GetProperty("activity_data_draws").GetUInt32();
            if(replacements!=1)throw new IOException($"Expected one token replacement, got {replacements}");
            Console.WriteLine("ACTIVITY_TOKEN_CHANGE_OK 123->9 exactly one region replaced");
            serial.NotifyHostGoingAway();serial.PauseTransmission();
            await Task.Delay(6000,stop.Token);
            var offline=serial.ReadDeviceInfo();
            var pages=offline.PageData!.Value;
            int width=pages.GetProperty("offline_clock_width").GetInt32(),height=pages.GetProperty("offline_clock_height").GetInt32();
            if(offline.BridgeOnline||offline.UsbActive||width>232||height<40)throw new IOException("Offline display mode/clock geometry failed.");
            Console.WriteLine($"OFFLINE_CLOCK_GEOMETRY_OK width={width} height={height} offline=true");
            serial.ResumeTransmission();await Task.Delay(2500,stop.Token);
            if(!serial.ReadDeviceInfo().UsbActive)throw new IOException("USB did not recover after offline preview.");
            Console.WriteLine("OFFLINE_USB_RECOVERY_OK");
        } finally {
            serial.ResumeTransmission();
            if(original is not null)serial.SendDisplayMode(original.Mode);
            stop.Cancel();try{await worker;}catch(OperationCanceledException)when(stop.IsCancellationRequested){}
        }
    }
}
