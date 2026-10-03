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
        var now=DateTimeOffset.Now;
        var statusSnapshot=new StatusSnapshot(1,"12:00",0,0,now,new("idle",null),new("idle",null)) with {Weather=new("北京","晴",20,25,15,40,0,null,null,"test",now,true),Stocks=new([],now,false)};
        var statusRegistry=new DeviceRegistry(1,true,false,false,[tab,esp with {Enabled=false}]);
        var statusDemand=new DeviceDataDemand(["weather","quotas"],["deepseek"]);
        var statusView=BridgeStatusView.Capture(statusRegistry,d=>new(d.Enabled,d.Enabled?"在线":"已停用","","0.2.35-ui",Transport:d.Enabled?"USB":null),statusSnapshot,statusDemand,null);
        Check(statusView.Data.Single(d=>d.Name=="天气").Status=="旧缓存","Stale data was presented as current");
        Check(statusView.Data.Single(d=>d.Name=="股票").Status=="未启用","Disabled source appeared enabled");
        Check(statusView.Data.Single(d=>d.Name=="Codex 额度").Status=="暂无数据","Missing data appeared current");
        foreach(float scale in new[]{1F,1.25F,1.5F,2F})using(var status=new BridgeStatusForm(()=>statusView)){
            status.Scale(new SizeF(scale,scale));Capture(status,directory,$"service-status-{scale:0.00}");status.Size=status.MinimumSize;Capture(status,directory,$"service-status-{scale:0.00}-minimum");
        }
        foreach(var (key,devices) in new (string,RegisteredDevice[])[]{("empty",[]),("tab5",[tab]),("esp8266",[esp]),("both",[esp,tab]),("disabled",[tab with {Enabled=false}])}) {
            var store=new DeviceRegistryStore(Path.Combine(directory,key+".json"));store.Migrate(null,false,false);foreach(var d in devices)store.Add(d);
            string? target=null,action=null;
            using(var menu=DeviceCenterMenu.Build(store.Snapshot,(id,a)=>{target=id;action=a;},a=>action=a)) {
                var roots=menu.Items.OfType<ToolStripMenuItem>().ToArray();
                Check(roots.Length==2+devices.Length+(devices.Any(d=>d.Kind==HardwareKind.Esp8266&&d.Enabled)?1:0),"Tray menu contains unexpected entries");
                menu.Items["devices"]!.PerformClick();Check(action=="devices","Device center entry lost");
                menu.Items["exit"]!.PerformClick();Check(action=="exit","Exit entry lost");
                var preview=menu.Items["mirror"];
                if(devices.SingleOrDefault(d=>d.Kind==HardwareKind.Esp8266&&d.Enabled) is {} screen){preview!.PerformClick();Check(target==screen.Id&&action=="mirror","Tray preview targeted wrong device");}
                else Check(preview is null,"Preview shown without an enabled screen");
                foreach(var d in devices){
                    var group=(ToolStripMenuItem)menu.Items["device:"+d.Id]!;
                    Check(group.Enabled==d.Enabled&&group.Text?.StartsWith(d.Name)==true,"Device name or disabled state lost");
                    string[] expected=d.Kind==HardwareKind.Esp8266?["cycle","appearance","data","legacy-settings"]:["voice","birthday-settings","data","tab5"];
                    Check(group.DropDownItems.Cast<ToolStripItem>().Select(i=>i.Name).SequenceEqual(expected),"Device shortcuts or order changed");
                    foreach(var entry in group.DropDownItems.OfType<ToolStripMenuItem>()){
                        Check(entry.DropDownItems.Count==0,"Tray menu exceeds two levels");
                        if(!d.Enabled)continue;
                        Check(DeviceCapabilities.Allows(d,entry.Name??""),"Unsupported device shortcut");
                        target=null;action=null;entry.PerformClick();Check(target==d.Id&&action==entry.Name,"Shortcut routed to another device");
                    }
                }
                Check(menu.Items["accounts"] is null,"Shared account settings leaked into device shortcuts");
                menu.Show(new Point(-30000,-30000));Application.DoEvents();
                using(var shot=new Bitmap(menu.Width,menu.Height)){menu.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(directory,$"tray-{key}.png"));}
                if(key=="both")foreach(var group in roots.Where(i=>i.DropDownItems.Count>0)){
                    group.ShowDropDown();Application.DoEvents();
                    using var shot=new Bitmap(menu.Width+group.DropDown.Width,Math.Max(menu.Height,group.Bounds.Top+group.DropDown.Height));
                    menu.DrawToBitmap(shot,new Rectangle(Point.Empty,menu.Size));group.DropDown.DrawToBitmap(shot,new Rectangle(menu.Width,group.Bounds.Top,group.DropDown.Width,group.DropDown.Height));
                    shot.Save(Path.Combine(directory,"tray-group-"+devices.Single(d=>"device:"+d.Id==group.Name).Kind+".png"));group.HideDropDown();
                }
                action=null;menu.Items["devices"]!.PerformClick();Application.DoEvents();
                Check(action=="devices"&&!menu.Visible,"Native menu did not close before opening destination");
            }
            foreach(float scale in new[]{1F,1.25F,1.5F,2F})using(var form=new DeviceCenterForm(store,d=>new(false,"离线",d.Kind==HardwareKind.Tab5?"USB：未连接  Wi-Fi：未连接  蓝牙：未连接\n当前通道：无":"USB：未连接  Wi-Fi：未连接","待连接读取","未连接","未连接",d.Kind==HardwareKind.Tab5?"未连接":null,"无"),(id,a)=>{target=id;action=a;},(_,_)=>Task.CompletedTask,()=>{},a=>action=a)) {
                form.Scale(new SizeF(scale,scale));Capture(form,directory,$"{key}-{scale:0.00}");
                if(devices.Length>1){var list=Descendants(form).OfType<ListBox>().Single();list.SelectedIndex=1;Application.DoEvents();Check(Descendants(form).OfType<Button>().Any(b=>b.Text=="语音设置"),"Switch did not show TAB5 capabilities");Check(!Descendants(form).OfType<Button>().Any(b=>b.Text=="画面预览"),"ESP action leaked into TAB5");}
                form.Size=form.MinimumSize;Capture(form,directory,$"{key}-{scale:0.00}-minimum");
                foreach(var card in Descendants(Descendants(form).OfType<TabControl>().Single().SelectedTab!).OfType<DeviceActionButton>())
                    Check(TextRenderer.MeasureText(card.AccessibleDescription,card.Font,Size.Empty,TextFormatFlags.SingleLine).Width<=card.ClientSize.Width-24,"Device card subtitle truncated at minimum size: "+card.Text);
                form.ShowPage("accounts");Capture(form,directory,$"{key}-{scale:0.00}-accounts");
                var tabs=Descendants(form).OfType<TabControl>().Single();
                Check(tabs.SelectedTab?.Name=="accounts","Shared settings did not navigate inside main window");
                form.Show();Application.DoEvents();
                foreach(var (label,route) in new[]{("模型账号","authorize"),("天气定位","weather-settings"),("自选股票","stocks-settings"),("额度历史","quota-trend")}) {
                    Descendants(tabs.SelectedTab!).OfType<DeviceActionButton>().Single(b=>b.Text==label).PerformClick();Check(action==route,"Shared action route lost: "+label);
                }
                form.Reload();Check(tabs.SelectedTab?.Name=="accounts","Reload unexpectedly left shared settings");
                form.ShowPage("bridge-settings");Capture(form,directory,$"{key}-{scale:0.00}-bridge");
                form.Show();Application.DoEvents();
                foreach(var (label,route) in new[]{("开机启动","startup"),("服务状态","status"),("关于应用","about")}) {
                    Descendants(tabs.SelectedTab!).OfType<DeviceActionButton>().Single(b=>b.Text==label).PerformClick();Check(action==route,"Bridge action route lost: "+label);
                }
                form.ShowPage("devices");Check(Descendants(form).OfType<ListBox>().Single().SelectedItem is RegisteredDevice selected?devices.Any(d=>d.Id==selected.Id):devices.Length==0,"Device selection lost across navigation");
                form.Show();Application.DoEvents();
                foreach(var d in devices){
                    form.SelectDevice(d.Id);Check(((RegisteredDevice)Descendants(form).OfType<ListBox>().Single().SelectedItem!).Id==d.Id,"Direct device navigation selected wrong target");
                    var cards=Descendants(tabs.SelectedTab!).OfType<DeviceActionButton>().OrderBy(b=>b.PointToScreen(Point.Empty).Y).ThenBy(b=>b.PointToScreen(Point.Empty).X).Select(b=>b.Text).ToArray();
                    Check(cards.SequenceEqual(d.Kind==HardwareKind.Tab5?new[]{"显示设置","语音设置","日历生日","常用任务","数据设置","连接升级"}:new[]{"显示设置","外观设置","数据设置","连接设置"}),"Device action order or duplicate entry regressed");
                    var button=Descendants(form).OfType<Button>().Single(b=>b.Text==(d.Kind==HardwareKind.Tab5?"语音设置":"连接设置"));
                    Check(button.Enabled==d.Enabled,"Disabled device action enabled");
                    if(d.Enabled){button.PerformClick();Check(target==d.Id&&action==(d.Kind==HardwareKind.Tab5?"voice":"legacy-settings"),"Device center action targeted wrong device");}
                }
            }
        }
        var migrationStore=new DeviceRegistryStore(Path.Combine(directory,"migration-banner.json"));migrationStore.Migrate(tab.HardwareId,true,false);
        using(var migration=new DeviceCenterForm(migrationStore,_=>new(true,"在线","USB：已连接  Wi-Fi：未连接  蓝牙：已连接\n当前通道：USB","0.2.35-ui","已连接","未连接","已连接","USB"),(_,_)=>{},(_,_)=>Task.CompletedTask,()=>{},_=>{})){
            Capture(migration,directory,"migration-banner");migration.Size=migration.MinimumSize;Capture(migration,directory,"migration-banner-minimum");
            Check(Descendants(migration).OfType<Button>().Any(b=>b.Text=="导入 ESP8266"),"Legacy import no longer identifies the hardware");
        }
        foreach(var mode in Enum.GetValues<EspConnectionMode>())using(var legacy=new LegacyDeviceSettingsForm(mode:mode,view:()=>new(false,"离线","","",Usb:"未连接",Wifi:"未连接",Transport:"无"))) {
            Capture(legacy,directory,"legacy-settings-"+mode);legacy.Size=legacy.MinimumSize;Capture(legacy,directory,"legacy-settings-"+mode+"-minimum");
            Check(legacy.SelectedMode==mode,"Connection settings did not restore saved mode");
        }
        using(var voice=new Tab5VoiceHost(new Tab5VoiceSettings()))using(var settings=voice.CreateSettings(preview:true)){
            Capture(settings,directory,"voice-simple");settings.Show();Application.DoEvents();
            var advanced=Descendants(settings).Single(c=>c.Name=="voice-advanced");Check(!advanced.Visible,"Voice diagnostics shown by default");
            Descendants(settings).OfType<Button>().Single(b=>b.Name=="voice-troubleshoot").PerformClick();Check(advanced.Visible,"Voice troubleshooting did not expand");Capture(settings,directory,"voice-troubleshooting");
        }
        var names=new DeviceRegistryStore(Path.Combine(directory,"device-names.json"));names.Migrate(null,false,false);names.Add(tab with {Name="M5Stack TAB5"});names.Add(esp with {Name="ESP8266 小屏"});
        CheckDataSettings(names,directory);
        using(var namesForm=new DeviceCenterForm(names,_=>new(false,"离线","等待设备数据","待连接读取"),(_,_)=>{},(_,_)=>Task.CompletedTask,()=>{},_=>{})){
            Capture(namesForm,directory,"device-names");namesForm.Size=namesForm.MinimumSize;Capture(namesForm,directory,"device-names-minimum");
            var list=Descendants(namesForm).OfType<ListBox>().Single();foreach(var d in names.Snapshot.Devices)Check(list.ItemHeight>=TextRenderer.MeasureText(d.Name,list.Font,new Size(list.ClientSize.Width-16,int.MaxValue),TextFormatFlags.WordBreak).Height+14,"Device name vertically clipped");
        }
        using(var closedCenter=new DeviceCenterForm(names,_=>new(false,"离线","",""),(_,_)=>{},(_,_)=>Task.CompletedTask,()=>{},_=>{})) {
            closedCenter.Dispose();
            using var birthday=new BirthdaySettingsForm(preview:true){Opacity=0,ShowInTaskbar=false};
            birthday.Shown+=(_,_)=>birthday.BeginInvoke(()=>birthday.Close());
            Check(SettingsWindow.DialogOwner(closedCenter) is null,"Disposed device center retained as dialog owner");
            birthday.ShowDialog(SettingsWindow.DialogOwner(closedCenter));
            var area=Screen.FromPoint(Cursor.Position).WorkingArea;
            Check(Math.Abs(birthday.Left-(area.Left+(area.Width-birthday.Width)/2))<=1&&Math.Abs(birthday.Top-(area.Top+(area.Height-birthday.Height)/2))<=1,"Birthday dialog not centered on current screen");
        }
        foreach(var (name,registered) in new (string,RegisteredDevice[])[]{("new",[]),("add-esp",[tab]),("add-tab",[esp]),("full",[tab,esp]),("full-disabled",[tab,esp with {Enabled=false}])})
        using(var add=new AddDeviceForm(new(1,true,false,false,registered),preview:true)){
            Capture(add,directory,"add-device-"+name);
            if(registered.Length==2) {
                Check(!Descendants(add).OfType<ComboBox>().Any(),"Full registry still offered duplicate registration");
                Check(Descendants(add).OfType<Label>().Any(l=>l.Text.Contains("每种型号一台")),"Registration limit has no explanation");
                Check(add.Added is null&&add.CancelButton is not null,"Full registry modified a device or has no close action");
                foreach(float scale in new[]{1F,1.25F,1.5F,2F})using(var scaled=new AddDeviceForm(new(1,true,false,false,registered),preview:true)) {
                    scaled.Scale(new SizeF(scale,scale));Capture(scaled,directory,$"add-device-{name}-{scale:0.00}");
                    var close=(Button)scaled.CancelButton!;
                    Check(scaled.ClientRectangle.Contains(scaled.RectangleToClient(close.RectangleToScreen(close.ClientRectangle))),"Add limit close button clipped under scaling");
                    var content=Descendants(scaled).OfType<FlowLayoutPanel>().Single(p=>p.Name=="add-device-content");
                    Check(!content.VerticalScroll.Visible,"Add limit default layout requires scrolling");
                    scaled.Size=scaled.MinimumSize;Capture(scaled,directory,$"add-device-{name}-{scale:0.00}-minimum");
                    Check(scaled.ClientRectangle.Contains(scaled.RectangleToClient(close.RectangleToScreen(close.ClientRectangle))),"Add limit close button clipped at minimum size");
                }
                continue;
            }
            var choices=Descendants(add).OfType<ComboBox>().First();Check(choices.Items.Count==2-registered.Length,"Already registered model offered for addition");
            if(registered.Length==1)Check(Descendants(add).OfType<Button>().Any(b=>b.Text==(registered[0].Kind==HardwareKind.Tab5?"ESP8266 首次安装…":"TAB5 首次安装…")),"First installation missing for unregistered model");
        }
        using(var shared=new SettingsForm(BridgeSettings.CreatePublicSelfTestSettings(),false)){Capture(shared,directory,"shared-settings");Check(!Descendants(shared).OfType<Label>().Any(l=>l.Text.Contains("屏保")||l.Text.StartsWith("串口")),"Legacy fields exposed in shared settings");}
        File.WriteAllText(Path.Combine(directory,"result.txt"),"PASS: registry, migration, device menu matrix, fixed target actions, demand union, collector lifecycle, independent service cancellation, LAN gates, shared action cards, localized data settings persistence/cancel/device isolation, quota dependency and add-limit feedback, UI 100/125/150/200 percent scale and minimum bounds. Hardware acceptance pending.");
        Console.WriteLine("DEVICE_CENTER_OK menu/target/demand/services/LAN/layout; real hardware pending");
    }
    private static void CheckDataSettings(DeviceRegistryStore store,string directory) {
        var tab=store.Snapshot.Devices.Single(d=>d.Kind==HardwareKind.Tab5);
        var esp=store.Snapshot.Devices.Single(d=>d.Kind==HardwareKind.Esp8266);
        foreach(float scale in new[]{1F,1.25F,1.5F,2F})using(var form=new DeviceDataForm(store,tab.Id)) {
            form.Scale(new SizeF(scale,scale));Capture(form,directory,$"data-{scale:0.00}");
            form.Size=form.MinimumSize;Capture(form,directory,$"data-{scale:0.00}-minimum");
            var footer=Descendants(form).Single(c=>c.Name=="data-actions");
            Check(form.ClientRectangle.Contains(form.RectangleToClient(footer.RectangleToScreen(footer.ClientRectangle))),"Data settings footer clipped");
        }
        using(var form=new DeviceDataForm(store,tab.Id)) {
            form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.Show();Application.DoEvents();
            CheckBox Option(string name)=>Descendants(form).OfType<CheckBox>().Single(c=>c.Name==name);
            var models=Descendants(form).Single(c=>c.Name=="provider-options");
            Check(models.Enabled&&Option("provider-deepseek").Checked&&Option("provider-zhipu").Checked,"Saved providers not restored");
            Check(Descendants(models).OfType<CheckBox>().Count()==DeviceRegistryStore.ProviderIds.Length,"Provider choice lost");
            Option("source-quotas").Checked=false;
            Check(!models.Enabled&&Option("provider-deepseek").Checked,"Disabling quota lost remembered selection");
            Capture(form,directory,"data-quotas-disabled");form.Show();Application.DoEvents();
            Option("source-quotas").Checked=true;Option("provider-qwen").Checked=true;Option("provider-zhipu").Checked=false;Option("source-music").Checked=false;
            store.Update(tab with {Name="新设备名称"});
            Descendants(form).OfType<Button>().Single(b=>b.Name=="save-data").PerformClick();
            Check(form.DialogResult==DialogResult.OK,"Data settings did not save");
        }
        var saved=new DeviceRegistryStore(Path.Combine(directory,"device-names.json")).Snapshot.Devices;
        var updated=saved.Single(d=>d.Id==tab.Id);
        Check(updated.Name=="新设备名称"&&updated.Providers.SequenceEqual(new[]{"qwen","deepseek"})&&!updated.Sources.Contains("music"),"Localized UI did not persist stable IDs or overwrote latest metadata");
        var other=saved.Single(d=>d.Id==esp.Id);Check(other.Sources.SequenceEqual(esp.Sources)&&other.Providers.SequenceEqual(esp.Providers),"Saving TAB5 modified ESP selections");
        using(var form=new DeviceDataForm(store,tab.Id)) {
            form.ShowInTaskbar=false;form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.Show();Application.DoEvents();
            Descendants(form).OfType<CheckBox>().Single(c=>c.Name=="provider-deepseek").Checked=false;
            Descendants(form).OfType<Button>().Single(b=>b.Name=="cancel-data").PerformClick();
            Check(store.Snapshot.Devices.Single(d=>d.Id==tab.Id).Providers.SequenceEqual(updated.Providers),"Cancel saved changes");
        }
        store.Update(tab);
        bool clicked=false;
        using var center=new DeviceCenterForm(store,_=>new(false,"离线","",""),(_,_)=>{},(_,_)=>Task.CompletedTask,()=>clicked=true,_=>{});
        center.ShowInTaskbar=false;center.StartPosition=FormStartPosition.Manual;center.Location=new(-30000,-30000);center.Show();Application.DoEvents();
        var add=Descendants(center).OfType<Button>().Single(b=>b.Name=="add-device");Check(add.Enabled,"Add button silently disabled at device limit");add.PerformClick();Check(clicked,"Add button does not dispatch at device limit");
        Console.WriteLine("DEVICE_DATA_OK localized ID persistence, quota dependency, cancel, device isolation, add feedback");
    }
    private static async Task CheckServicesAsync() {
        var oldPair=new Tab5Pairing("001122334455",@"USB\VID_303A&PID_1001\OLD",Convert.ToBase64String(new byte[32]));
        var changed=new FlashUsbDevice("COM9",@"USB\VID_303A&PID_4005\NEW","TAB5");
        var other=new FlashUsbDevice("COM12",@"USB\VID_303A&PID_4005\OTHER","Other ESP32");
        var espPort=new FlashUsbDevice("COM7",@"USB\VID_1A86&PID_7523\ESP","ESP8266");
        var probed=new List<string>();
        var recovered=await Tab5UsbRecovery.FindAsync(oldPair,[espPort,other,changed],(d,_)=>{probed.Add(d.Port);return Task.FromResult(d==changed?oldPair.DeviceId:"998877665544");},CancellationToken.None);
        Check(recovered==changed&&!probed.Contains("COM7"),"USB recovery did not verify identity or touched ESP8266");
        Check(await Tab5UsbRecovery.FindAsync(oldPair,[other],(_,_)=>Task.FromResult("998877665544"),CancellationToken.None) is null,"Unrelated TAB5 was accepted");
        Check(await Tab5UsbRecovery.FindAsync(oldPair,[changed],(_,_)=>throw new TimeoutException(),CancellationToken.None) is null,"Unresponsive USB device was accepted");
        var starts=new ConcurrentDictionary<string,int>();var stops=new ConcurrentDictionary<string,int>();
        async Task Worker(string name,CancellationToken token){starts.AddOrUpdate(name,1,(_,v)=>v+1);try{await Task.Delay(Timeout.Infinite,token);}finally{stops.AddOrUpdate(name,1,(_,v)=>v+1);}}
        var tab=DeviceRegistryStore.Create(HardwareKind.Tab5,"TAB5","001122334455") with {Sources=["weather","quotas"],Providers=["zhipu"]};
        var esp=DeviceRegistryStore.Create(HardwareKind.Esp8266,"ESP8266",null) with {Sources=["stocks"],Providers=[]};
        var empty=new DeviceRegistry(1,true,false,false,[]);var both=empty with{Devices=[esp,tab]};var policy=DisplayModes.Load(BridgeSettings.CreatePublicSelfTestSettings(),"weather");
        var demand=DeviceDataDemand.From(both,policy,"qwen");Check(demand.Sources.SetEquals(["stocks","weather","quotas"])&&demand.Providers.SetEquals(["zhipu"]),"Device demand still coupled to ESP pages");
        Check(DeviceDataDemand.From(empty,policy,"qwen").Sources.SetEquals(["quotas"]),"Bridge startup must record quotas without hardware");
        Check(DeviceDataDemand.From(empty with {DesktopQuotaHistory=true},policy,"qwen").Sources.SetEquals(["quotas"]),"Desktop history lost without devices");
        using var runtime=new BridgeRuntime(false,Worker);await runtime.ApplyDemandAsync(demand);await Task.Delay(60);
        Check(runtime.ActiveCollectors.SequenceEqual(new[]{"quotas","stocks","weather"}),"Collector set mismatch");
        await runtime.ApplyDemandAsync(demand);await runtime.ApplyDemandAsync(new(["weather"],[]));Check(starts["weather"]==1&&stops["stocks"]==1&&stops["quotas"]==1&&!stops.ContainsKey("weather"),"Collector restart affected unchanged source");
        await runtime.ApplyDemandAsync(new([],[]));Check(runtime.ActiveCollectors.Length==0,"Last device leaves collectors running");
        var pairing=new Tab5PairingStore();pairing.Pair("998877665544",@"USB\VID_303A&PID_1001\OLDDEVICE");
        pairing.Pair(tab.HardwareId!,@"USB\VID_303A&PID_1001\DEVICECENTERTEST");
        var savedKey=pairing.Current!.Key;pairing.Pair(tab.HardwareId!,changed.Identity);Check(pairing.Current.Key==savedKey,"USB rebind rotated pairing key");
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
            foreach(var mode in new[]{EspConnectionMode.Wifi,EspConnectionMode.Usb,EspConnectionMode.Auto}) {
                await manager.ApplyAsync(both with {Devices=[esp with {ConnectionMode=mode},tab]});
                Check(manager.Serial.UsbDataEnabled==(mode!=EspConnectionMode.Wifi)&&manager.EspLanEnabled==(mode!=EspConnectionMode.Usb),"Selected transport gate mismatch");
                Check(ReferenceEquals(original,manager.Tab5)&&starts["esp-usb"]==1&&starts["tab5-usb"]==1&&starts["lan"]==1,"Mode switch restarted device services");
                var view=manager.View(esp);
                if(mode==EspConnectionMode.Wifi)Check(view.Usb=="未使用"&&view.Transport!="USB","Wi-Fi-only mode reported USB active");
                if(mode==EspConnectionMode.Usb)Check(view.Wifi=="未使用","USB-only mode reported Wi-Fi active");
            }
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
            Check((await http.GetAsync($"http://127.0.0.1:{port}/missing")).StatusCode==HttpStatusCode.NotFound&&activity==1,"Invalid route counted as a data heartbeat");
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
        foreach(var row in Descendants(form).OfType<TableLayoutPanel>().Where(r=>r.Visible&&Equals(r.Tag,"action-row"))){
            var button=row.Controls.OfType<Button>().Single();
            Check(button.Height<=Math.Max(button.MinimumSize.Height,button.Font.Height+button.Padding.Vertical+22),$"{name}: action button stretched vertically");
            Check(button.Width>=TextRenderer.MeasureText(button.Text,button.Font).Width+button.Padding.Horizontal,$"{name}: action title clipped");
        }
        using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new Rectangle(Point.Empty,form.Size));image.Save(Path.Combine(directory,name+".png"));form.Hide();
    }
    private static IEnumerable<Control> Descendants(Control parent){foreach(Control c in parent.Controls){yield return c;foreach(var child in Descendants(c))yield return child;}}
}
