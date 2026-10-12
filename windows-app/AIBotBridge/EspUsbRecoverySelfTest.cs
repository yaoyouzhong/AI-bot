namespace AIBotBridge;

internal static class EspUsbRecoverySelfTest
{
    internal static void Run() {
        void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
        const string old=@"USB\VID_1A86&PID_7523\OLD",id="esp8266-aabbccddeeff";
        var moved=new FlashUsbDevice("COM8",@"USB\VID_1A86&PID_7523\NEW","CH340");
        var other=new FlashUsbDevice("COM10",@"USB\VID_1A86&PID_7523\OTHER","CH340");
        Check(EspUsbRecovery.Candidates(old,id,[moved],null).SequenceEqual([moved]),"Moved adapter not discovered");
        Check(EspUsbRecovery.Candidates(old,id,[moved,other],null).Count==2,"Multiple adapters cannot be probed by ID");
        Check(EspUsbRecovery.Candidates(old,id,[moved,new("COM7",old,"bound")],null).SequenceEqual([moved]),"Occupied old socket hid moved matching device");
        Check(EspUsbRecovery.Candidates(old,id,[moved],"COM8").Count==0,"Reserved port was selected");
        Check(EspUsbRecovery.Candidates(old,null,[moved],null).Count==0,"Legacy firmware triggered unverified rebinding");
        Check(EspUsbRecovery.Candidates(old,id,[moved with{Identity=@"USB\VID_303A&PID_4005\TAB5"}],null).Count==0,"Different model selected");
        Check(EspUsbRecovery.Matches(id,id)&&!EspUsbRecovery.Matches(id,"esp8266-112233445566")&&!EspUsbRecovery.Matches(id,null),"Stable identity check failed");
        Check(!EspUsbRecovery.ValidId("esp8266-000000000000")&&!EspUsbRecovery.ValidId("esp8266-ffffffffffff")&&!EspUsbRecovery.ValidId("esp8266-AABBCCDDEEFF"),"Malformed ID accepted");
        Check(SerialPublisher.TryParsePong("@AIBOT {\"version\":1,\"type\":\"pong\",\"device\":\"esp8266\",\"ip\":\"\",\"device_id\":\""+id+"\"}",out var ip,out _,out var parsed)&&ip is null&&parsed==id,"Offline USB identity lost");
        Check(SerialPublisher.TryParsePong("@AIBOT {\"version\":1,\"type\":\"pong\",\"device\":\"esp8266\"}",out _,out _,out parsed)&&parsed is null,"Legacy pong no longer compatible");
        string folder=Path.Combine(Path.GetTempPath(),"aibot-usb-recovery-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try {
            var store=new DeviceRegistryStore(Path.Combine(folder,"devices.json"));store.Migrate(null,false,false);
            var esp=DeviceRegistryStore.Create(HardwareKind.Esp8266,"小屏",null) with{UsbIdentity=old,EspDeviceId=id};store.Add(esp);
            Check(!store.AcceptEspUsbIdentity(esp.Id,old,moved,"esp8266-112233445566"),"Wrong device overwrote registry");
            Check(store.AcceptEspUsbIdentity(esp.Id,old,moved,id),"Matched device binding not saved");
            store.Update(esp with{Name="改名"});var saved=new DeviceRegistryStore(Path.Combine(folder,"devices.json")).Snapshot.Devices.Single();
            Check(saved.UsbIdentity==moved.Identity&&saved.EspDeviceId==id&&saved.Name=="改名","Stale settings save reverted recovery");
            Check(!store.AcceptEspUsbIdentity(esp.Id,old,other,id),"Stale recovery overwrote new binding");
            store.Update(saved with{Enabled=false});Check(!store.AcceptEspUsbIdentity(esp.Id,moved.Identity,other,id),"Disabled device rebound");
        } finally {Directory.Delete(folder,true);}
        Console.WriteLine("ESP_USB_RECOVERY_OK offline USB ID, changed/multiple/reserved adapters, wrong-ID rejection, legacy compatibility, persistent rebinding and stale-save protection");
    }

    // A real UART test, with the resident bridge yielding its handle. The test
    // uses an absent instance path and starts no LAN/discovery worker or pairing.
    // It does not edit the user's registry or send a LAN configuration.
    internal static async Task RunHardwareAsync() {
        AppProfileGuard.EnsureDesktopProfile();
        var device=new DeviceRegistryStore().Snapshot.Devices.Single(d=>d.Kind==HardwareKind.Esp8266&&d.Enabled);
        if(!EspUsbRecovery.ValidId(device.EspDeviceId)||device.UsbIdentity is null)throw new IOException("请先在已确认 USB 连接登记固定设备 ID。");
        using var http=new HttpClient(new SocketsHttpHandler{UseProxy=false}){Timeout=TimeSpan.FromSeconds(15)};
        var snapshot=System.Text.Json.JsonSerializer.Deserialize<StatusSnapshot>(await http.GetStringAsync("http://127.0.0.1:18765/status"),JsonDefaults.Options)??throw new IOException("桥接状态无效。");
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var lease=await FlashUsbLease.AcquireAsync(stop.Token);
        string[] parts=device.UsbIdentity.Split('\\');string missing=parts[0]+"\\"+parts[1]+"\\OFFLINE-RECOVERY-TEST";
        if(FlashDeviceDiscovery.Read().Any(d=>d.Identity==missing))throw new IOException("测试路径意外存在。");
        bool rebound=false;
        var serial=new SerialPublisher(null){ExpectedUsbIdentity=missing,ExpectedEspDeviceId=device.EspDeviceId,
            AcceptUsbIdentity=(previous,usb,id)=>rebound=previous==missing&&EspUsbRecovery.Matches(device.EspDeviceId,id)};
        var worker=Task.Run(()=>serial.RunAsync(()=>snapshot,()=>[],stop.Token));
        try {
            while(!serial.UsbDataActive){if(worker.IsCompleted)await worker;await Task.Delay(100,stop.Token);}
            var first=serial.ReadDeviceInfo();await Task.Delay(4500,stop.Token);var next=serial.ReadDeviceInfo();
            if(!rebound||!EspUsbRecovery.Matches(device.EspDeviceId,next.DeviceId)||!next.UsbActive||!next.BridgeOnline||next.UsbStatusCount<=first.UsbStatusCount||serial.CurrentPairing is not null||serial.ConfiguredLanHost is not null)
                throw new IOException("无 LAN 配置的实际 USB 重新识别／同步测试未通过。");
            Console.WriteLine($"ESP_USB_RECOVERY_HARDWARE_OK firmware={next.Firmware} port={serial.PortName} same_id=true absent_registered_path=true no_lan_configuration=true usb_frames={first.UsbStatusCount}->{next.UsbStatusCount}; actual UART, simulated missing instance path, device Wi-Fi not disabled");
        } finally {
            stop.Cancel();try{await worker;}catch(OperationCanceledException) when(stop.IsCancellationRequested){}
            await lease.ReleaseAsync(restoreCycle:true);
        }
    }
}
