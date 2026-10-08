using System.IO.Ports;
using System.Reflection;
using System.Text.Json;

namespace AIBotBridge;
internal static class EspUpdateSelfTest
{
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    internal static (DeviceServiceManager Manager, RegisteredDevice Device) Fixture() {
        var manager=new DeviceServiceManager(null!,0,CancellationToken.None);
        var device=DeviceRegistryStore.Create(HardwareKind.Esp8266,"Test ESP",null);
        return (manager,device);
    }
    internal static void Usb(DeviceServiceManager manager,string? version) {
        var json=JsonSerializer.SerializeToElement(new {version=1,type="pong",device="esp8266",firmware=version});
        string wire=version is null?"@AIBOT {\"version\":1,\"type\":\"pong\",\"device\":\"esp8266\"}":"@AIBOT "+json.GetRawText();
        Check(SerialPublisher.TryParsePong(wire,out _,out string? firmware),"ESP pong rejected");
        Set(manager.Serial,"_portName","COM7");Set(manager.Serial,"_lastStatusWrittenAt",Environment.TickCount64);Set(manager.Serial,"_usbFirmwareVersion",firmware);
    }
    private static void Set(object value,string name,object? field)=>value.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(value,field);
    internal static void Run() {
        var (manager,device)=Fixture();
        var package=new UpdateAsset("AI-bot-0.6.0-firmware-materials.zip",new Uri("https://github.com/yaoyouzhong/AI-bot/releases/download/esp8266-v0.6.0/AI-bot-0.6.0-firmware-materials.zip"),1);
        var release=new ComponentUpdate("esp8266","0.6.0",new Version(0,6,0),"",package,package);
        UpdateDevice Capture()=>UpdateDevice.From(device,manager.View(device));
        Check(UpdateService.PreparationBlocked(Capture(),release) is not null,"Unconnected target allowed");
        Usb(manager,null);var legacy=Capture();
        Check(legacy.Online&&legacy.Version==EspFirmwareVersion.Legacy,"Production device view lost legacy identity");
        Check(UpdateService.Blocked(legacy,release) is not null&&UpdateService.PreparationBlocked(legacy,release) is null,"Legacy migration is not distinct from upgrade");
        Check(UpdateReminder.Pending([legacy],new Dictionary<string,ComponentUpdate>{{"esp8266",release}},new()).Length==0,"Legacy unknown falsely notified as newer");
        Check(UpdateService.PreparationBlocked(legacy with {Online=false},release) is not null,"Offline legacy accepted");
        Check(UpdateService.PreparationBlocked(legacy with {Enabled=false},release) is not null,"Disabled legacy accepted");
        Check(UpdateService.PreparationBlocked(legacy,release with {Checksums=null}) is not null,"Legacy bypassed checksum gate");
        Usb(manager,"0.5.0");var older=Capture();Check(UpdateService.Blocked(older,release) is null,"Reported old firmware not updatable");
        Check(!UpdateService.SameTarget(legacy,older,release),"Version changed during download accepted");
        Usb(manager,"0.6.0");Check(UpdateService.PreparationBlocked(Capture(),release)=="已是最新版本","Equal firmware allowed");
        Usb(manager,"0.7.0");Check(UpdateService.PreparationBlocked(Capture(),release)=="当前版本较新，保留现有版本","Downgrade allowed");
        foreach(string bad in new[]{"","v0.5.0","01.2.3","1.2.3.4","-1.2.3",new string('9',40)}) {
            Usb(manager,bad);Check(Capture().Version==EspFirmwareVersion.Invalid&&UpdateService.PreparationBlocked(Capture(),release) is not null,"Malformed version treated as legacy");
        }
        Set(manager.Serial,"_portName",null);Set(manager,"<EspEnabled>k__BackingField",true);Set(manager,"_legacyLanAt",Environment.TickCount64);
        manager.Serial.ObserveLanFirmware("0.5.0");Check(Capture().Version=="0.5.0","Wi-Fi version not used by production view");
        manager.Serial.ObserveLanFirmware(null);Check(UpdateService.IsLegacyEsp(Capture()),"Legacy Wi-Fi missing header not handled");
        manager.Serial.SetPairing(null);Check(Capture().Version=="待连接读取","Pairing reset retained firmware");
        Set(manager,"_legacyLanAt",0);Check(!Capture().Online&&UpdateService.PreparationBlocked(Capture(),release) is not null,"Disconnected identity reused");
        Console.WriteLine("ESP_UPDATE_OK production pong/view/selection, legacy manual-only, known/equal/newer/invalid/offline/changed versions, Wi-Fi and identity reset");
    }

    // Read-only physical acceptance: the normal bridge lease releases USB first.
    // No reset, backup, erase or write command is sent to the ESP8266.
    internal static async Task ReadHardwareAsync(string portName,string output) {
        var device=FlashDeviceDiscovery.Read().Single(d=>d.Port.Equals(portName,StringComparison.OrdinalIgnoreCase));
        if(device.Identity.Contains("VID_303A",StringComparison.OrdinalIgnoreCase))throw new IOException("TAB5 is not an ESP8266 update target");
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var lease=await FlashUsbLease.AcquireAsync(stop.Token);
        try {
            using var port=new SerialPort(portName,460800){NewLine="\n",ReadTimeout=3000,WriteTimeout=2000,DtrEnable=false,RtsEnable=false};
            FlashDeviceSelection.RequireSame(device,FlashDeviceDiscovery.Read());
            port.Open();port.DiscardInBuffer();port.WriteLine("@AIBOT {\"version\":1,\"type\":\"ping\"}");
            string? version=null;bool received=false;
            var until=DateTime.UtcNow.AddSeconds(5);
            while(DateTime.UtcNow<until){if(SerialPublisher.TryParsePong(port.ReadLine().Trim(),out _,out version)){received=true;break;}}
            Check(received,"No real ESP8266 pong");
            var (manager,target)=Fixture();Set(manager.Serial,"_portName",portName);Set(manager.Serial,"_lastStatusWrittenAt",Environment.TickCount64);Set(manager.Serial,"_usbFirmwareVersion",version);
            var row=UpdateDevice.From(target,manager.View(target));
            using var updates=new UpdateService();await updates.CheckAsync(stop.Token);var release=updates.Available["esp8266"];
            string? blocked=UpdateService.PreparationBlocked(row,release);
            Directory.CreateDirectory(output);
            await File.WriteAllTextAsync(Path.Combine(output,"hardware-readonly.json"),JsonSerializer.Serialize(new {port=portName,firmware=row.Version,row.Online,available=release.Version,manual=UpdateService.IsLegacyEsp(row),blocked,hardwareWritten=false},new JsonSerializerOptions{WriteIndented=true}));
            Check(blocked is null||blocked=="已是最新版本"||blocked=="当前版本较新，保留现有版本","Physical target update route blocked: "+blocked);
            Console.WriteLine("ESP_HARDWARE_READONLY_OK "+row.Version+"; available="+release.Version+"; write=false");
        }finally{await lease.ReleaseAsync(false);}
    }
}
