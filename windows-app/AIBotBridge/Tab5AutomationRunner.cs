using System.Text;
using System.Text.Json;

namespace AIBotBridge;

// Explicit paired-device diagnostics. No tray, foreground window or implicit touch.
internal static class Tab5AutomationRunner
{
    internal sealed record Backend(Func<JsonElement,CancellationToken,Task<Tab5UIAutomation.Result>> Action,
        Func<CancellationToken,Task<Tab5ScreenPreview.Frame>> Capture,
        Func<CancellationToken,Task<Tab5DisplaySettings>> Settings,Func<CancellationToken,Task> Wake,
        Func<string,CancellationToken,Task<Tab5ScreenPreview.Frame>>? CaptureTransport=null);

    internal sealed class InputActivity(Func<long?> actual,Func<long> clock) {
        private long _synthetic=-1;
        internal void Wake()=>Interlocked.Exchange(ref _synthetic,clock());
        internal long? Read(){
            long now=clock(),synthetic=Interlocked.Read(ref _synthetic);long? real=actual();
            if(synthetic<0||synthetic>now)return real;
            return real is {} value?Math.Max(value,synthetic):synthetic;
        }
    }

    internal static void Run(string directory,Tab5UsbDiagnostic? suite=null,string? screenCheck=null)
    {
        using var instance=new Mutex(false,@"Local\AIBotBridge.Instance."+System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value);
        bool owned;try{owned=instance.WaitOne(0);}catch(AbandonedMutexException){owned=true;}
        if(!owned)throw new InvalidOperationException("Normally exit the resident bridge before USB diagnostics.");
        try{
            if(suite is null&&screenCheck is null)RunWithMessageLoop(()=>RunAsync(Path.GetFullPath(directory),suite,screenCheck));
            else RunAsync(Path.GetFullPath(directory),suite,screenCheck).GetAwaiter().GetResult();
        }finally{instance.ReleaseMutex();}
    }
    // The voice dispatcher and its timers require a pumping STA even when the
    // diagnostic host has no window. Blocking this thread strands both the
    // first async continuation and later voice commands.
    internal static void RunWithMessageLoop(Func<Task> run)
    {
        var previous=SynchronizationContext.Current;
        using var loop=new ApplicationContext();
        using var start=new System.Windows.Forms.Timer{Interval=1};
        System.Runtime.ExceptionServices.ExceptionDispatchInfo? failure=null;
        start.Tick+=async(_,_)=>{
            start.Stop();
            try{await run();}
            catch(Exception error){failure=System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error);}
            finally{loop.ExitThread();}
        };
        try{start.Start();Application.Run(loop);}
        finally{SynchronizationContext.SetSynchronizationContext(previous);}
        failure?.Throw();
    }
    private static async Task RunAsync(string directory,Tab5UsbDiagnostic? suite,string? screenCheck)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("New output directory required");
        var registry=new DeviceRegistryStore().Snapshot;
        var device=registry.Devices.SingleOrDefault(d=>d.Kind==HardwareKind.Tab5&&d.Enabled)??throw new IOException("TAB5 unavailable");
        var store=new Tab5PairingStore();
        if(store.Current?.DeviceId!=device.HardwareId)throw new IOException("TAB5 pairing unavailable");
        Directory.CreateDirectory(directory);
        using var stop=new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var runtime=new BridgeRuntime(startRefresh:false);
        var observed=new List<object>();
        var activity=new InputActivity(SystemIdleTime.LastInputTickMilliseconds,()=>Environment.TickCount64);
        using var service=new Tab5Service(store,acceptance:suite){
            DiagnosticInputClock=activity.Read,
            ActivitySourceEnabled=device.Sources.Contains("activity"),
            DraftObservation=screenCheck is "prepare" or "sync"?(client,op,task,request,status)=>{
                lock(observed)if(observed.Count<128)observed.Add(new{client,op,task,request,status});
            }:null
        };
        if(suite is not null)await suite.PrepareAsync(service,device.HardwareId??throw new IOException("Missing paired device identity"),stop.Token);
        else if(screenCheck is null)service.AttachVoice(new Tab5VoiceHost(physicalToggle:service.TryVoiceHidToggle));
        var installed=BridgeInstallation.Read(Path.ChangeExtension(typeof(Tab5AutomationRunner).Assembly.Location,".exe"),BridgeElevation.Owner);
        int port=int.TryParse(Environment.GetEnvironmentVariable("AIBOT_HTTP_PORT"),out int configured)&&configured is >0 and <=65535
            ?configured:installed?.HttpPort??BridgeFirewall.DefaultHttpPort;
        var serial=new SerialPublisher(null,null); // No ESP worker is started.
        var server=new LocalStatusServer(port);
        List<Task> workers=[Task.Run(()=>service.RunUsbAsync(stop.Token)),
            Task.Run(()=>Tab5StatusPublisher.RunAsync(()=>service.Publish(runtime.Capture()),stop.Token,TimeSpan.FromMilliseconds(400)))];
        if(suite is null&&screenCheck is null){
            var discovery=new LanDiscoveryServer{Tab5Response=service.Discover,LegacyEnabled=()=>false};
            workers.Add(Task.Run(()=>discovery.RunAsync(stop.Token)));
            workers.Add(Task.Run(()=>LanBindingManager.RunAsync(port,serial,runtime.Capture,runtime.Resources,stop.Token,
                selectAddress:hint=>LanPairingFactory.FindPrivateAddress(service.UsbWifiPeer??hint),
                discovery:discovery,tab5:service,legacyEnabled:()=>false)));
            workers.Add(Task.Run(()=>server.RunAsync(runtime.Capture,stop.Token)));
        }
        try
        {
            bool UsbReady()=>service.ScreenPreviewContext?.Transports.Contains("USB")==true;
            using(var connect=CancellationTokenSource.CreateLinkedTokenSource(stop.Token)){
                connect.CancelAfter(TimeSpan.FromSeconds(45));
                while(!(UsbReady()&&service.UsbUiAutomationActive)&&!(suite is null&&screenCheck is null&&service.WifiUiAutomationAvailable)){
                    if(workers.Any(t=>t.IsFaulted))throw new IOException("Diagnostic worker unavailable");
                    await Task.Delay(100,connect.Token);
                }
            }
            string initialTransport=UsbReady()&&service.UsbUiAutomationActive?"USB":"Wi-Fi";
            async Task<Tab5UIAutomation.Result> Action(JsonElement command,CancellationToken token)
            {
                string transport=command.TryGetProperty("transport",out var selected)?selected.GetString()??"": "USB";
                string op=command.GetProperty("op").GetString()??"";
                if(transport=="USB"&&!UsbReady())throw new IOException("Authenticated USB required");
                if((transport=="Wi-Fi"||op=="wifi-test")&&!service.WifiUiAutomationAvailable)
                    throw new IOException("Authenticated Wi-Fi automation required");
                string owner=service.UiAutomationOwner??throw new IOException("TAB5 pairing unavailable");
                string request=service.UIAutomation.Begin(command,transport:transport,owner:owner);
                try{
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(8));
                    while(true){timeout.Token.ThrowIfCancellationRequested();var result=service.UIAutomation.Snapshot();
                        if(result?.Request==request&&result.Status!=1){if(result.Status!=2)throw new IOException("UI action rejected");await Task.Delay(400,token);return result;}
                        await Task.Delay(30,timeout.Token);}
                }finally{service.UIAutomation.Cancel();}
            }
            async Task<Tab5ScreenPreview.Frame> CaptureOn(string transport,CancellationToken token)
            {
                Tab5ScreenPreview.Context? Context()=>service.ScreenPreviewContext is {} live
                    ?live with{Transports=live.Transports.Contains(transport)?[transport]:[]}:null;
                string lease=service.ScreenPreview.Start(Context());
                try{
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(35));
                    while(true){timeout.Token.ThrowIfCancellationRequested();service.ScreenPreview.Renew(lease,Context());
                        if(service.ScreenPreview.Snapshot(lease).Frame is {} frame)return frame;await Task.Delay(50,timeout.Token);}
                }finally{service.ScreenPreview.Stop(lease);}
            }
            using var input=new StreamReader(Console.OpenStandardInput(),Encoding.UTF8);
            using var output=new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)){AutoFlush=true};
            var backend=new Backend(Action,t=>CaptureOn("USB",t),
                t=>ReadSettingsAsync(ct=>service.DisplaySettingsAsync(null,ct),()=>service.Busy,t),
                async t=>{activity.Wake();await Task.Delay(1500,t);},CaptureOn);
            if(screenCheck is not null){
                var before=await backend.Settings(stop.Token);
                var viewport=await Action(JsonSerializer.SerializeToElement(new{op="probe"}),stop.Token);
                if(viewport.Width!=1280||viewport.Height!=720)throw new IOException("unexpected_viewport");
                await new Tab5NativeScreen(backend,viewport,directory).CheckClientSwitch(screenCheck,stop.Token);
                if(screenCheck=="prepare")await Task.Delay(TimeSpan.FromSeconds(18),stop.Token);
                if(JsonSerializer.Serialize(before)!=JsonSerializer.Serialize(await backend.Settings(stop.Token)))throw new IOException("settings_changed");
            }
            else if(suite is null)await CommandsAsync(input,output,directory,backend,stop.Token,initialTransport);
            else await suite.RunAsync(backend,stop.Token);
        }
        finally{
            if(screenCheck is "prepare" or "sync")lock(observed)File.WriteAllText(Path.Combine(directory,"draft-traffic.json"),JsonSerializer.Serialize(observed));
            service.UIAutomation.Cancel();service.ScreenPreview.Stop();stop.Cancel();
            try{await Task.WhenAll(workers).WaitAsync(TimeSpan.FromSeconds(10));}catch(Exception e) when(e is OperationCanceledException or IOException or TimeoutException){}
        }
    }
    internal static async Task<Tab5DisplaySettings> ReadSettingsAsync(
        Func<CancellationToken,Task<Tab5DisplaySettings>> read,Func<bool> busy,CancellationToken token)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(TimeSpan.FromSeconds(30));
        while(true){
            deadline.Token.ThrowIfCancellationRequested();
            if(busy()){await Task.Delay(50,deadline.Token);continue;}
            try{return await read(deadline.Token);}
            catch(InvalidOperationException) when(busy()){
                // A new RPC may start between the idle check and the read.
                await Task.Delay(50,deadline.Token);
            }
        }
    }
    internal static string CapturePath(string directory,string name)
    {
        if(Encoding.UTF8.GetByteCount(name)>80||name.StartsWith('.')||!name.EndsWith(".png",StringComparison.OrdinalIgnoreCase)||name.Any(c=>!char.IsLetterOrDigit(c)&&c is not ('-' or '_' or '.')))
            throw new ArgumentException("Invalid capture filename");
        return Path.Combine(directory,name);
    }
    internal static async Task CommandsAsync(TextReader input,TextWriter output,string directory,Backend backend,
        CancellationToken token,string initialTransport="USB")
    {
        async Task Emit(object value){await output.WriteLineAsync(JsonSerializer.Serialize(value));await output.FlushAsync(token);}
        var before=await backend.Settings(token);
        string transport=initialTransport;
        var viewport=await backend.Action(JsonSerializer.SerializeToElement(new{op="probe",transport}),token);
        await Emit(new{status="ready",width=viewport.Width,height=viewport.Height,rotation=viewport.Rotation,transport});
        var reader=new Tab5BoundedLines(input,4096);
        bool stopped=false,restorePending=false,modeRestored=true;

        async Task RestoreAsync(CancellationToken restoreToken)
        {
            try{_ =await backend.Action(JsonSerializer.SerializeToElement(new{op="restore",transport="Wi-Fi"}),restoreToken);}
            catch(Exception e) when(e is IOException or TimeoutException or OperationCanceledException){}
            // An authenticated USB gesture probe works only after the device
            // has left the temporary Wi-Fi route, even if its Wi-Fi ACK was lost.
            viewport=await backend.Action(JsonSerializer.SerializeToElement(new{op="probe",transport="USB"}),restoreToken);
            transport="USB";
            restorePending=false;
        }

        try {
            while(!token.IsCancellationRequested){
                var line=await reader.ReadAsync().WaitAsync(token);if(line is null)break;
                try{
                    using var json=JsonDocument.Parse(line);var value=json.RootElement;
                    if(value.ValueKind!=JsonValueKind.Object||!value.TryGetProperty("op",out var op)||op.ValueKind!=JsonValueKind.String)throw new ArgumentException("Invalid command");
                    string operation=op.GetString()!;
                    if(operation=="stop"){stopped=true;break;}
                    if(operation=="wifi-test"){
                        if(transport!="USB"||restorePending)throw new InvalidOperationException("A temporary Wi-Fi test is already active or USB is not selected");
                        restorePending=true;
                        await backend.Action(JsonSerializer.SerializeToElement(new{op=operation,transport="USB"}),token);
                        viewport=await backend.Action(JsonSerializer.SerializeToElement(new{op="probe",transport="Wi-Fi"}),token);
                        if(!viewport.WifiTestActive)throw new IOException("Temporary Wi-Fi route not confirmed");
                        transport="Wi-Fi";
                        await Emit(new{status="done",op=operation,transport,wifiTestActive=true});
                        continue;
                    }
                    if(operation=="restore"){
                        if(!restorePending)throw new InvalidOperationException("No temporary Wi-Fi test");
                        await RestoreAsync(token);
                        await Emit(new{status="done",op=operation,transport,wifiTestActive=false});
                        continue;
                    }
                    string route=value.TryGetProperty("transport",out var selected)&&selected.ValueKind==JsonValueKind.String
                        ?selected.GetString()!:transport;
                    if(route is not ("USB" or "Wi-Fi"))throw new ArgumentException("Invalid automation transport");
                    if(operation=="wake"){
                        await backend.Wake(token);
                        await Emit(new{status="wake-requested"});
                        continue;
                    }
                    if(operation=="capture"){
                        string file=value.TryGetProperty("file",out var f)&&f.ValueKind==JsonValueKind.String?f.GetString()!:"";
                        string path=CapturePath(directory,file);
                        var frame=await (backend.CaptureTransport is {} capture?capture(route,token):backend.Capture(token));
                        using var image=Tab5ScreenPreviewForm.Render(frame);
                        using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write))image.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
                        await Emit(new{status="captured",file,transport=route,width=frame.Width,height=frame.Height});
                    }else{
                        var command=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(line)!;
                        command["transport"]=JsonSerializer.SerializeToElement(route);
                        command.TryAdd("width",JsonSerializer.SerializeToElement(viewport.Width));command.TryAdd("height",JsonSerializer.SerializeToElement(viewport.Height));command.TryAdd("rotation",JsonSerializer.SerializeToElement(viewport.Rotation));
                        viewport=await backend.Action(JsonSerializer.SerializeToElement(command),token);
                        await Emit(new{status="done",op=operation,transport=route,width=viewport.Width,height=viewport.Height,rotation=viewport.Rotation});
                    }
                }catch(Exception e) when(e is JsonException or ArgumentException or IOException or InvalidOperationException or TimeoutException){await Emit(new{status="error",error=e.GetType().Name});}
            }
        }finally {
            if(restorePending){
                using var recovery=new CancellationTokenSource(TimeSpan.FromSeconds(20));
                try{await RestoreAsync(recovery.Token);}
                catch(Exception e) when(e is IOException or TimeoutException or OperationCanceledException){modeRestored=false;}
            }
        }
        bool preserved=JsonSerializer.Serialize(before)==JsonSerializer.Serialize(await backend.Settings(token));
        await Emit(new{status="stopped",settingsPreserved=preserved,modeRestored,inputEnded=!stopped});
        if(!preserved||!modeRestored)throw new IOException("Device settings restoration not confirmed");
    }
}
