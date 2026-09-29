namespace AIBotBridge;

// Exclusive diagnostic: run with the normal bridge stopped, then restart it.
// Modes are applied in memory; device registration and preferences are never saved.
internal static class EspTransportHardwareTest
{
    internal static async Task RunAsync(int port)
    {
        if(port is <1 or >65535)throw new ArgumentOutOfRangeException(nameof(port));
        if(System.Diagnostics.Process.GetProcessesByName("AIBotBridge").Any(p=>p.Id!=Environment.ProcessId))
            throw new InvalidOperationException("请先正常退出桥接，再执行独占连接检查。");
        var registry=new DeviceRegistryStore().Snapshot;
        var esp=registry.Devices.SingleOrDefault(d=>d.Kind==HardwareKind.Esp8266&&d.Enabled)
            ??throw new InvalidOperationException("未登记已启用的小屏。");
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(100));
        using var runtime=new BridgeRuntime(startRefresh:false);
        runtime.SetDisplayPolicy(DisplayModes.Load(BridgeSettings.Load()));
        var manager=new DeviceServiceManager(runtime,port,stop.Token);
        Task Select(EspConnectionMode mode)=>manager.ApplyAsync(registry with {Devices=[esp with {ConnectionMode=mode}]});
        async Task Until(Func<bool> ready,int seconds) {
            long end=Environment.TickCount64+seconds*1000;
            while(!ready()){if(Environment.TickCount64>=end)throw new TimeoutException("连接未在预期时间内就绪。");await Task.Delay(250,stop.Token);}
        }
        UsbDeviceInfo Read()=>manager.Serial.ReadDeviceInfo();
        static void Check(bool ok,string text,UsbDeviceInfo info) {
            if(!ok)throw new IOException($"{text}: USB={info.UsbActive}, online={info.BridgeOnline}, usb={info.UsbStatusCount}, lan={info.LanStatusCount}");
        }
        static bool Fresh(UsbDeviceInfo info)=>info.PageData is {} page&&page.TryGetProperty("display_cached",out var cache)&&!cache.GetBoolean()
            &&page.TryGetProperty("rendered_page",out var rendered)&&rendered.GetString()!="offline";
        try {
            await Select(EspConnectionMode.Usb);await Until(()=>manager.Serial.UsbDataActive,30);
            var usbStart=Read();await Task.Delay(3000,stop.Token);var usb=Read();
            Check(usb.UsbActive&&usb.BridgeOnline&&usb.UsbStatusCount>usbStart.UsbStatusCount&&!manager.EspLanEnabled,"USB-only",usb);
            Console.WriteLine($"ESP_USB_ONLY_OK usb_delta={usb.UsbStatusCount-usbStart.UsbStatusCount} lan_enabled=false");
            await Select(EspConnectionMode.Wifi);var beforeWifi=Read();
            await Task.Delay(14000,stop.Token);var wifi=Read();
            Check(!wifi.UsbActive&&wifi.BridgeOnline&&wifi.UsbStatusCount==beforeWifi.UsbStatusCount&&wifi.LanStatusCount>beforeWifi.LanStatusCount&&wifi.UptimeMs>=usb.UptimeMs&&Fresh(wifi),"Wi-Fi-only",wifi);
            Check(manager.View(esp).Transport=="Wi-Fi","Wi-Fi display state",wifi);
            await Task.Delay(5000,stop.Token);var wifiLater=Read();
            Check(!wifiLater.UsbActive&&wifiLater.BridgeOnline&&wifiLater.UsbStatusCount==wifi.UsbStatusCount&&wifiLater.LanStatusCount>wifi.LanStatusCount&&Fresh(wifiLater),"Wi-Fi continuous data",wifiLater);
            Console.WriteLine($"ESP_WIFI_ONLY_OK usb_delta=0 lan_delta={wifiLater.LanStatusCount-beforeWifi.LanStatusCount} live_page=true");
            var pairing=manager.Serial.CurrentPairing??throw new IOException("Wi-Fi 配对状态未就绪。");
            await Select(EspConnectionMode.Usb);await Until(()=>manager.Serial.UsbDataActive,12);
            using(var http=new HttpClient{Timeout=TimeSpan.FromSeconds(4)}) {
                http.DefaultRequestHeaders.Add("X-AIBot-Token",pairing.Token);
                foreach(string route in new[]{"/status","/resources"}) {
                    using var response=await http.GetAsync($"http://{pairing.Address}:{pairing.Port}{route}",stop.Token);
                    if(response.StatusCode!=System.Net.HttpStatusCode.NotFound)throw new IOException("仅 USB 模式仍提供小屏 Wi-Fi 数据。");
                }
            }
            Console.WriteLine("ESP_USB_ONLY_LAN_BLOCKED_OK status/resources=404");
            await Select(EspConnectionMode.Auto);await Until(()=>manager.Serial.UsbDataActive,12);var automatic=Read();
            Check(automatic.UsbActive&&automatic.BridgeOnline&&automatic.UsbStatusCount>wifiLater.UsbStatusCount&&manager.View(esp).Transport=="USB","Automatic USB recovery",automatic);
            Console.WriteLine("ESP_AUTO_OK usb_recovered=true");
            await WifiFallbackTest.RunAsync(manager.Serial,stop.Token);
            Console.WriteLine("ESP_TRANSPORTS_OK USB-only, Wi-Fi-only, automatic fallback and recovery; no preferences saved");
        }
        finally {
            manager.Serial.UsbDataEnabled=true;manager.Serial.ResumeTransmission();
            stop.Cancel();await manager.StopAsync();
        }
    }
}
