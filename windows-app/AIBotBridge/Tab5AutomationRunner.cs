using System.Text;
using System.Text.Json;

namespace AIBotBridge;

// Explicit USB diagnostics only. No tray, foreground window or implicit touch.
internal static class Tab5AutomationRunner
{
    internal sealed record Backend(Func<JsonElement,CancellationToken,Task<Tab5UIAutomation.Result>> Action,
        Func<CancellationToken,Task<Tab5ScreenPreview.Frame>> Capture,
        Func<CancellationToken,Task<Tab5DisplaySettings>> Settings);

    internal static void Run(string directory)
    {
        using var instance=new Mutex(false,@"Local\AIBotBridge.Instance."+System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value);
        bool owned;try{owned=instance.WaitOne(0);}catch(AbandonedMutexException){owned=true;}
        if(!owned)throw new InvalidOperationException("Normally exit the resident bridge before USB diagnostics.");
        try{RunAsync(Path.GetFullPath(directory)).GetAwaiter().GetResult();}finally{instance.ReleaseMutex();}
    }
    private static async Task RunAsync(string directory)
    {
        if(Directory.Exists(directory)||File.Exists(directory))throw new IOException("New output directory required");
        var registry=new DeviceRegistryStore().Snapshot;
        var device=registry.Devices.SingleOrDefault(d=>d.Kind==HardwareKind.Tab5&&d.Enabled)??throw new IOException("TAB5 unavailable");
        var store=new Tab5PairingStore();
        if(store.Current?.DeviceId!=device.HardwareId)throw new IOException("TAB5 pairing unavailable");
        Directory.CreateDirectory(directory);
        using var stop=new CancellationTokenSource(TimeSpan.FromMinutes(10));
        using var runtime=new BridgeRuntime(startRefresh:false);
        using var service=new Tab5Service(store){ActivitySourceEnabled=device.Sources.Contains("activity")};
        service.AttachVoice(new Tab5VoiceHost(physicalToggle:service.TryVoiceHidToggle));
        int port=int.TryParse(Environment.GetEnvironmentVariable("AIBOT_HTTP_PORT"),out int configured)&&configured is >0 and <=65535?configured:8765;
        var serial=new SerialPublisher(null,null); // No ESP worker is started.
        var server=new LocalStatusServer(port);
        Task[] workers=[Task.Run(()=>service.RunUsbAsync(stop.Token)),
            Task.Run(()=>Tab5StatusPublisher.RunAsync(()=>service.Publish(runtime.Capture()),stop.Token,TimeSpan.FromMilliseconds(400))),
            Task.Run(()=>LanBindingManager.RunAsync(port,serial,runtime.Capture,runtime.Resources,stop.Token,tab5:service,legacyEnabled:()=>false)),
            Task.Run(()=>server.RunAsync(runtime.Capture,stop.Token))];
        try
        {
            using(var connect=CancellationTokenSource.CreateLinkedTokenSource(stop.Token)){
                connect.CancelAfter(TimeSpan.FromSeconds(45));
                while(service.ScreenPreviewContext?.Transports.Contains("USB")!=true){if(workers.Any(t=>t.IsFaulted))throw new IOException("Diagnostic worker unavailable");await Task.Delay(100,connect.Token);}
            }
            async Task<Tab5UIAutomation.Result> Action(JsonElement command,CancellationToken token)
            {
                if(service.ScreenPreviewContext?.Transports.Contains("USB")!=true)throw new IOException("Authenticated USB required");
                string request=service.UIAutomation.Begin(command);
                try{
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(8));
                    while(true){timeout.Token.ThrowIfCancellationRequested();var result=service.UIAutomation.Snapshot();
                        if(result?.Request==request&&result.Status!=1){if(result.Status!=2)throw new IOException("UI action rejected");await Task.Delay(400,token);return result;}
                        await Task.Delay(30,timeout.Token);}
                }finally{service.UIAutomation.Cancel();}
            }
            async Task<Tab5ScreenPreview.Frame> Capture(CancellationToken token)
            {
                string lease=service.ScreenPreview.Start(service.ScreenPreviewContext);
                try{
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(35));
                    while(true){timeout.Token.ThrowIfCancellationRequested();service.ScreenPreview.Renew(lease,service.ScreenPreviewContext);
                        if(service.ScreenPreview.Snapshot(lease).Frame is {} frame)return frame;await Task.Delay(50,timeout.Token);}
                }finally{service.ScreenPreview.Stop(lease);}
            }
            using var input=new StreamReader(Console.OpenStandardInput(),Encoding.UTF8);
            using var output=new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)){AutoFlush=true};
            await CommandsAsync(input,output,directory,new(Action,Capture,t=>ReadSettingsAsync(
                ct=>service.DisplaySettingsAsync(null,ct),()=>service.Busy,t)),stop.Token);
        }
        finally{
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
    internal static async Task CommandsAsync(TextReader input,TextWriter output,string directory,Backend backend,CancellationToken token)
    {
        async Task Emit(object value){await output.WriteLineAsync(JsonSerializer.Serialize(value));await output.FlushAsync(token);}
        var before=await backend.Settings(token);
        var viewport=await backend.Action(JsonSerializer.SerializeToElement(new{op="probe"}),token);
        await Emit(new{status="ready",width=viewport.Width,height=viewport.Height,rotation=viewport.Rotation});
        var reader=new Tab5BoundedLines(input,4096);
        bool stopped=false;
        while(!token.IsCancellationRequested){
            var line=await reader.ReadAsync().WaitAsync(token);if(line is null)break;
            try{
                using var json=JsonDocument.Parse(line);var value=json.RootElement;
                if(value.ValueKind!=JsonValueKind.Object||!value.TryGetProperty("op",out var op)||op.ValueKind!=JsonValueKind.String)throw new ArgumentException("Invalid command");
                string operation=op.GetString()!;
                if(operation=="stop"){stopped=true;break;}
                if(operation=="capture"){
                    string file=value.TryGetProperty("file",out var f)&&f.ValueKind==JsonValueKind.String?f.GetString()!:"";
                    string path=CapturePath(directory,file);
                    var frame=await backend.Capture(token);
                    using var image=Tab5ScreenPreviewForm.Render(frame);
                    using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write))image.Save(stream,System.Drawing.Imaging.ImageFormat.Png);
                    await Emit(new{status="captured",file,width=frame.Width,height=frame.Height});
                }else{
                    var command=JsonSerializer.Deserialize<Dictionary<string,JsonElement>>(line)!;
                    command.TryAdd("width",JsonSerializer.SerializeToElement(viewport.Width));command.TryAdd("height",JsonSerializer.SerializeToElement(viewport.Height));command.TryAdd("rotation",JsonSerializer.SerializeToElement(viewport.Rotation));
                    viewport=await backend.Action(JsonSerializer.SerializeToElement(command),token);
                    await Emit(new{status="done",op=operation,width=viewport.Width,height=viewport.Height,rotation=viewport.Rotation});
                }
            }catch(Exception e) when(e is JsonException or ArgumentException or IOException or InvalidOperationException or TimeoutException){await Emit(new{status="error",error=e.GetType().Name});}
        }
        bool preserved=JsonSerializer.Serialize(before)==JsonSerializer.Serialize(await backend.Settings(token));
        await Emit(new{status="stopped",settingsPreserved=preserved,inputEnded=!stopped});
        if(!preserved)throw new IOException("Device settings changed during diagnostics");
    }
}
