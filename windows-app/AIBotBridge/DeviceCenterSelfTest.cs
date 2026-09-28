using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace AIBotBridge;

internal static class DeviceCenterSelfTest
{
    private static void Check(bool ok,string message){if(!ok)throw new InvalidOperationException(message);}
    internal static void Run(string directory) {
        AppPaths.BeginPublicSelfTest();Directory.CreateDirectory(directory);DeviceRegistrySelfTest.Run();
        const string usb=@"USB\VID_1A86&PID_7523\REGISTERED";
        var ports=new[]{new FlashUsbDevice("COM12",usb,"known"),new FlashUsbDevice("COM9",@"USB\VID_1A86&PID_7523\OTHER","other")};
        Check(SerialPublisher.BoundPorts(usb,ports,()=>["COM9"]).SequenceEqual(["COM12"]),"Bound USB did not follow COM renumbering");
        Check(SerialPublisher.BoundPorts(usb,[ports[1]],()=>["COM9"]).Count==0,"Unrelated device reused old COM binding");
        Check(SerialPublisher.BoundPorts(null,ports,()=>["COM9"]).SequenceEqual(["COM9"]),"Legacy explicit port compatibility changed");
        Exception? failure=null;
        using(var runner=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000),Size=new(200,100)}) {
            runner.Shown+=async(_,_)=>{try{await CheckServicesAsync();await CheckLanAsync();}catch(Exception ex){failure=ex;}finally{runner.Close();}};
            Application.Run(runner);
        }
        if(failure is not null)throw failure;
        var tab=DeviceRegistryStore.Create(HardwareKind.Tab5,"书桌平板","001122334455");var esp=DeviceRegistryStore.Create(HardwareKind.Esp8266,"办公室小屏",null);
        foreach(var (key,devices) in new (string,RegisteredDevice[])[]{("empty",[]),("tab5",[tab]),("esp8266",[esp]),("both",[esp,tab]),("disabled",[tab with {Enabled=false}])}) {
            var store=new DeviceRegistryStore(Path.Combine(directory,key+".json"));store.Migrate(null,false,false);foreach(var d in devices)store.Add(d);
            string? target=null,action=null;
            using(var menu=DeviceCenterMenu.Build(store.Snapshot,_=>{},(id,a)=>{target=id;action=a;})) {
                Check(menu.Items.Cast<ToolStripItem>().Select(i=>i.Text).SequenceEqual(new[]{"我的设备","账号与数据源","桥接设置","关于","退出"}),"Unexpected top-level menu");
                var roots=((ToolStripMenuItem)menu.Items[0]).DropDownItems.OfType<ToolStripMenuItem>().Where(i=>i.Tag is string).ToArray();Check(roots.Length==devices.Length,"Unregistered device exposed");
                foreach(var device in devices){var item=roots.Single(i=>Equals(i.Tag,device.Id));Check(item.DropDownItems.Cast<ToolStripItem>().All(i=>i.Enabled==device.Enabled),"Disabled menu action enabled");
                    var first=(ToolStripMenuItem)item.DropDownItems[0];first.PerformClick();if(device.Enabled)Check(target==device.Id&&action==DeviceCapabilities.Actions(device.Kind)[0].Action,"Menu target lost");}
            }
            foreach(float scale in new[]{1F,1.25F,1.5F,2F})using(var form=new DeviceCenterForm(store,d=>new(false,"离线",d.Kind==HardwareKind.Tab5?"USB：未连接  Wi-Fi：未连接  蓝牙：未连接":"USB：未连接  Wi-Fi：未连接","待连接读取"),(id,a)=>{target=id;action=a;},(_,_)=>Task.CompletedTask,()=>{},()=>{})) {
                form.Scale(new SizeF(scale,scale));Capture(form,directory,$"{key}-{scale:0.00}");
                if(devices.Length>1){var list=Descendants(form).OfType<ListBox>().Single();list.SelectedIndex=1;Application.DoEvents();Check(Descendants(form).OfType<Button>().Any(b=>b.Text=="语音设置"),"Switch did not show TAB5 capabilities");Check(!Descendants(form).OfType<Button>().Any(b=>b.Text=="设备镜像"),"ESP action leaked into TAB5");}
                form.Size=form.MinimumSize;Capture(form,directory,$"{key}-{scale:0.00}-minimum");
            }
        }
        using(var legacy=new LegacyDeviceSettingsForm())Capture(legacy,directory,"legacy-settings");
        foreach(var (name,registered) in new (string,RegisteredDevice[])[]{("new",[]),("add-esp",[tab]),("add-tab",[esp])})
        using(var add=new AddDeviceForm(new(1,true,false,false,registered),preview:true)){
            Capture(add,directory,"add-device-"+name);
            var choices=Descendants(add).OfType<ComboBox>().First();Check(choices.Items.Count==2-registered.Length,"Already registered model offered for addition");
            if(registered.Length==1)Check(Descendants(add).OfType<Button>().Any(b=>b.Text==(registered[0].Kind==HardwareKind.Tab5?"ESP8266 首次安装…":"TAB5 首次安装…")),"First installation missing for unregistered model");
        }
        using(var shared=new SettingsForm(BridgeSettings.CreatePublicSelfTestSettings(),false)){Capture(shared,directory,"shared-settings");Check(!Descendants(shared).OfType<Label>().Any(l=>l.Text.Contains("屏保")||l.Text.StartsWith("串口")),"Legacy fields exposed in shared settings");}
        File.WriteAllText(Path.Combine(directory,"result.txt"),"PASS: registry, migration, device menu matrix, fixed target actions, demand union, collector lifecycle, independent service cancellation, LAN gates, UI 100/125/150/200 percent scale and minimum bounds. Hardware acceptance pending.");
        Console.WriteLine("DEVICE_CENTER_OK menu/target/demand/services/LAN/layout; real hardware pending");
    }
    private static async Task CheckServicesAsync() {
        var starts=new ConcurrentDictionary<string,int>();var stops=new ConcurrentDictionary<string,int>();
        async Task Worker(string name,CancellationToken token){starts.AddOrUpdate(name,1,(_,v)=>v+1);try{await Task.Delay(Timeout.Infinite,token);}finally{stops.AddOrUpdate(name,1,(_,v)=>v+1);}}
        var tab=DeviceRegistryStore.Create(HardwareKind.Tab5,"TAB5","001122334455") with {Sources=["weather","quotas"],Providers=["zhipu"]};
        var esp=DeviceRegistryStore.Create(HardwareKind.Esp8266,"ESP8266",null) with {Sources=["stocks"],Providers=[]};
        var empty=new DeviceRegistry(1,true,false,false,[]);var both=empty with{Devices=[esp,tab]};var policy=DisplayModes.Load(BridgeSettings.CreatePublicSelfTestSettings(),"weather");
        var demand=DeviceDataDemand.From(both,policy,"qwen");Check(demand.Sources.SetEquals(["stocks","weather","quotas"])&&demand.Providers.SetEquals(["zhipu"]),"Device demand still coupled to ESP pages");
        Check(DeviceDataDemand.From(empty,policy,"qwen").Sources.Count==0,"Empty profile starts collectors");
        Check(DeviceDataDemand.From(empty with {DesktopQuotaHistory=true},policy,"qwen").Sources.SetEquals(["quotas"]),"Desktop history lost without devices");
        using var runtime=new BridgeRuntime(false,Worker);await runtime.ApplyDemandAsync(demand);await Task.Delay(60);
        Check(runtime.ActiveCollectors.SequenceEqual(new[]{"quotas","stocks","weather"}),"Collector set mismatch");
        await runtime.ApplyDemandAsync(demand);await runtime.ApplyDemandAsync(new(["weather"],[]));Check(starts["weather"]==1&&stops["stocks"]==1&&stops["quotas"]==1&&!stops.ContainsKey("weather"),"Collector restart affected unchanged source");
        await runtime.ApplyDemandAsync(new([],[]));Check(runtime.ActiveCollectors.Length==0,"Last device leaves collectors running");
        var pairing=new Tab5PairingStore();pairing.Pair("998877665544",@"USB\VID_303A&PID_1001\OLDDEVICE");
        pairing.Pair(tab.HardwareId!,@"USB\VID_303A&PID_1001\DEVICECENTERTEST");
        string folder=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),"AI-bot");
        var backup=Directory.GetFiles(folder,"tab5-pairing.dat.*.bak").Single();Check(new Tab5PairingStore(backup).Current?.DeviceId=="998877665544","Replacement pairing did not preserve encrypted recovery backup");
        using var stop=new CancellationTokenSource();var manager=new DeviceServiceManager(runtime,0,stop.Token,Worker);
        try {
            await manager.ApplyAsync(empty);Check(manager.ActiveServices.Length==0&&manager.Tab5 is null,"Empty registry started hardware service");
            await manager.ApplyAsync(empty with {Devices=[tab]});await Task.Delay(60);
            Check(manager.ActiveServices.ToHashSet().SetEquals(["tab5","lan"])&&manager.Tab5 is not null&&!starts.ContainsKey("esp-usb"),"TAB5-only started ESP loop");
            var original=manager.Tab5!;
            original.OtaTransferActive(true);try{await manager.ApplyAsync(empty);throw new Exception("Busy OTA removal accepted");}catch(InvalidOperationException){}finally{original.OtaTransferActive(false);}
            await manager.ApplyAsync(both);await Task.Delay(60);Check(ReferenceEquals(original,manager.Tab5)&&starts["tab5-usb"]==1&&starts["lan"]==1,"Adding ESP restarted TAB5 or LAN");
            await manager.ApplyAsync(both with {Devices=[esp with{Enabled=false},tab]});Check(stops["esp-usb"]==1&&!stops.ContainsKey("tab5-usb"),"Disabling ESP stopped TAB5");
            await manager.ApplyAsync(empty);Check(manager.Tab5 is null&&manager.ActiveServices.Length==0&&stops["tab5-usb"]==1&&stops["tab5-ble"]==1&&stops["lan"]==1,"Device removal leaked service");
        }finally{await manager.StopAsync();}
        Console.WriteLine("DEVICE_SERVICES_OK independent lifetimes, no duplicate starts, cancellation releases all workers");
    }
    private static async Task CheckLanAsync() {
        var probe=new TcpListener(IPAddress.Loopback,0);probe.Start();int port=((IPEndPoint)probe.LocalEndpoint).Port;probe.Stop();bool enabled=false;int activity=0;
        using var stop=new CancellationTokenSource(TimeSpan.FromSeconds(10));var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var server=new LanStatusServer(new(IPAddress.Loopback,port,"test-token"),legacyEnabled:()=>enabled,legacyActivity:()=>activity++);
        var task=server.RunAsync(()=>new(1,"12:00",0,0,DateTimeOffset.Now,new("idle",null),new("idle",null)),stop.Token,()=>ready.SetResult());await ready.Task;
        using var http=new HttpClient();http.DefaultRequestHeaders.Add("X-AIBot-Token","test-token");
        try {
            Check((await http.GetAsync($"http://127.0.0.1:{port}/status")).StatusCode==HttpStatusCode.NotFound&&activity==0,"Disabled ESP LAN route available");
            enabled=true;Check((await http.GetAsync($"http://127.0.0.1:{port}/status")).IsSuccessStatusCode&&activity==1,"Enabled ESP route unavailable");
            enabled=false;Check((await http.GetAsync($"http://127.0.0.1:{port}/resources")).StatusCode==HttpStatusCode.NotFound,"Disabled ESP resources available");
            Check((await http.GetAsync($"http://127.0.0.1:{port}/tab5/v1/status")).StatusCode==HttpStatusCode.Unauthorized,"Unregistered TAB5 route accepted");
        }finally{stop.Cancel();await task;}
        Console.WriteLine("DEVICE_LAN_OK dynamic routing blocks disabled or unregistered devices");
    }
    private static void Capture(Form form,string directory,string name) {
        form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.Show();Application.DoEvents();form.PerformLayout();
        foreach(var c in Descendants(form).Where(c=>c.Visible))if(c.Left < -2||c.Right>c.Parent!.ClientSize.Width+2)throw new InvalidOperationException($"{name}: clipped {c.GetType().Name} {c.Text} {c.Bounds} parent {c.Parent!.ClientSize}");
        foreach(var button in Descendants(form).OfType<Button>().Where(b=>b.Visible&&!b.AutoSize)){
            var text=TextRenderer.MeasureText(button.Text,button.Font,new Size(Math.Max(20,button.Width-button.Padding.Horizontal-12),int.MaxValue),TextFormatFlags.WordBreak);
            Check(button.Height>=text.Height+button.Padding.Vertical,$"{name}: clipped button text {button.Text}");
        }
        using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(directory,name+".png"));form.Hide();
    }
    private static IEnumerable<Control> Descendants(Control parent){foreach(Control c in parent.Controls){yield return c;foreach(var child in Descendants(c))yield return child;}}
}
