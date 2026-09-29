namespace AIBotBridge;

// Device workers have independent lifetimes. Editing one device never restarts the other.
internal sealed class DeviceServiceManager
{
    private readonly BridgeRuntime _runtime;
    private readonly CancellationToken _shutdown;
    private CancellationTokenSource? _espStop,_tabStop,_lanStop;
    private Task[] _espTasks=[],_tabTasks=[],_lanTasks=[];
    private readonly int _port;
    private readonly Func<string,CancellationToken,Task>? _worker;
    internal string[] ActiveServices=>new[]{(_espStop,"esp8266"),(_tabStop,"tab5"),(_lanStop,"lan")}.Where(x=>x.Item1 is not null).Select(x=>x.Item2).ToArray();
    private Task Start(string name,Func<CancellationToken,Task> run,CancellationToken token)=>Task.Run(()=>_worker is null?run(token):_worker(name,token));
    private readonly SemaphoreSlim _change=new(1,1);
    internal SerialPublisher Serial {get;}
    internal Tab5Service? Tab5 {get;private set;}
    internal bool EspEnabled {get;private set;}
    private volatile EspConnectionMode _espMode;
    internal bool EspLanEnabled=>EspEnabled&&_espMode!=EspConnectionMode.Usb;
    internal bool Busy=>Serial.FlashBusy||Tab5?.Busy==true;
    internal string? Error {get;private set;}
    private long _legacyLanAt,_espStarted,_tabStarted;
    internal DeviceServiceManager(BridgeRuntime runtime,int port,CancellationToken shutdown,Func<string,CancellationToken,Task>? worker=null) {
        _worker=worker;
        _runtime=runtime;_port=port;_shutdown=shutdown;Serial=new(null,BridgeSettings.Load().Get("serial_port"));
        Serial.ReservedPort=()=>Tab5?.ReservedPort;
    }
    internal async Task ApplyAsync(DeviceRegistry registry) {
        await _change.WaitAsync(_shutdown);
        try {
            var espDevice=registry.Devices.SingleOrDefault(d=>d.Enabled&&d.Kind==HardwareKind.Esp8266);
            bool esp=espDevice is not null;
            var mode=espDevice?.ConnectionMode??EspConnectionMode.Auto;
            var tab=registry.Devices.SingleOrDefault(d=>d.Enabled&&d.Kind==HardwareKind.Tab5);
            if((EspEnabled&&!esp||Tab5 is not null&&tab is null)&&Busy)throw new InvalidOperationException("设备正在录音、升级或安装，请完成后再停用或移除。");
            if(mode!=_espMode&&Serial.FlashBusy)throw new InvalidOperationException("小屏正在升级，请完成后再切换连接方式。");
            if(mode!=_espMode){_espStarted=Environment.TickCount64;Interlocked.Exchange(ref _legacyLanAt,0);}
            _espMode=mode;Serial.UsbDataEnabled=mode!=EspConnectionMode.Wifi;
            if(!esp&&_espStop is not null){EspEnabled=false;await StopAsync(_espStop,_espTasks);_espStop=null;Serial.SetPairing(null);Interlocked.Exchange(ref _legacyLanAt,0);}
            if(tab is null&&_tabStop is not null){var old=Tab5;old?.BeginStop();Tab5=null;await StopAsync(_tabStop,_tabTasks);_tabStop=null;old?.Dispose();}
            if(esp&&_espStop is null) {
                Serial.ExpectedUsbIdentity=registry.Devices.Single(d=>d.Kind==HardwareKind.Esp8266).UsbIdentity;Serial.ReloadPort();_espStarted=Environment.TickCount64;EspEnabled=true;_espStop=CancellationTokenSource.CreateLinkedTokenSource(_shutdown);var token=_espStop.Token;
                _espTasks=[Start("esp-usb",ct=>Serial.RunAsync(_runtime.Capture,_runtime.Resources,ct),token),Start("esp-metrics",ct=>Serial.RunMetricsAsync(()=>_runtime.SystemMetrics,ct),token)];
            }
            Exception? failed=null;
            if(tab is not null&&_tabStop is null) {
                try{
                var store=new Tab5PairingStore();if(store.Current?.DeviceId!=tab.HardwareId)throw new InvalidOperationException("TAB5 配对与设备清单不一致，请保留配对资料并重新连接。");
                var service=new Tab5Service(store);service.AttachVoice(Environment.GetEnvironmentVariable("AIBOT_TAB5_MIC_DIAGNOSTIC")=="1"?new Tab5MicDiagnostic():new Tab5VoiceHost(physicalToggle:service.TryVoiceHidToggle));Tab5=service;_tabStarted=Environment.TickCount64;
                service.SystemMetricsCapture=()=>_runtime.SystemMetrics;
                _tabStop=CancellationTokenSource.CreateLinkedTokenSource(_shutdown);var token=_tabStop.Token;
                _tabTasks=[Start("tab5-usb",service.RunUsbAsync,token),Start("tab5-ble",service.RunBleAsync,token)];
                }catch(Exception ex){Tab5?.Dispose();Tab5=null;failed=ex;}
            }
            if(tab is not null&&Tab5 is not null)Tab5.WindowTitle=tab.Name;
            if((esp||Tab5 is not null)&&_lanStop is null) {
                _lanStop=CancellationTokenSource.CreateLinkedTokenSource(_shutdown);var token=_lanStop.Token;
                var discovery=new LanDiscoveryServer{Tab5Response=(request,address)=>Tab5?.Discover(request,address),LegacyEnabled=()=>EspLanEnabled};
                _lanTasks=[Start("discovery",discovery.RunAsync,token),Start("lan",ct=>LanBindingManager.RunAsync(_port,Serial,_runtime.Capture,_runtime.Resources,ct,discovery:discovery,
                    tab5Provider:()=>Tab5,legacyEnabled:()=>EspLanEnabled,legacyActivity:()=>Interlocked.Exchange(ref _legacyLanAt,Environment.TickCount64)),token)];
            }
            if(!esp&&tab is null&&_lanStop is not null){await StopAsync(_lanStop,_lanTasks);_lanStop=null;}
            if(failed is not null)throw failed;
            Error=null;
        }catch(Exception ex){Error=ex.Message;throw;}finally{_change.Release();}
    }
    internal DeviceView View(RegisteredDevice device) {
        if(!device.Enabled)return new(false,"已停用","连接服务已停止","待连接读取");
        var tasks=device.Kind==HardwareKind.Tab5?_tabTasks:_espTasks;
        if(tasks.Any(t=>t.IsFaulted))return new(false,"连接异常","连接服务遇到错误，请停用后重新启用设备。","待连接读取");
        if(device.Kind==HardwareKind.Tab5){var view=Tab5?.DeviceView??new(false,Error is null?"待配置":"连接异常",Error??"请连接或检查配对资料","待连接读取");return Tab5 is not null&&!view.Online&&view.Status=="离线"&&Environment.TickCount64-_tabStarted<15000?view with {Status="连接中"}:view;}
        bool usb=Serial.UsbDataActive,lan=EspLanEnabled&&Interlocked.Read(ref _legacyLanAt)>0&&Environment.TickCount64-Interlocked.Read(ref _legacyLanAt)<15000;
        string usbStatus=_espMode==EspConnectionMode.Wifi?"未使用":usb?"已连接":Serial.ConnectionStatus;
        string wifiStatus=_espMode==EspConnectionMode.Usb?"未使用":lan?"已连接":"未连接";
        return new(usb||lan,usb||lan?"在线":Environment.TickCount64-_espStarted<15000?"连接中":"离线",$"USB：{usbStatus}  Wi-Fi：{wifiStatus}","兼容协议 v1",usbStatus,wifiStatus,null,usb?"USB":lan?"Wi-Fi":"无");
    }
    private static async Task StopAsync(CancellationTokenSource stop,Task[] tasks) {
        stop.Cancel();try{await Task.WhenAll(tasks);}catch(OperationCanceledException) when(stop.IsCancellationRequested){}finally{stop.Dispose();}
    }
    internal async Task StopAsync(){await _change.WaitAsync();try{if(_lanStop is not null){await StopAsync(_lanStop,_lanTasks);_lanStop=null;}if(_espStop is not null){await StopAsync(_espStop,_espTasks);_espStop=null;}if(_tabStop is not null){await StopAsync(_tabStop,_tabTasks);_tabStop=null;}Tab5?.Dispose();Tab5=null;EspEnabled=false;}finally{_change.Release();}}
}
