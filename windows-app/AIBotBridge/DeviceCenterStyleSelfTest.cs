using System.Net;
using System.Text.Json;
namespace AIBotBridge;

// Real Forms with an isolated profile and offline callbacks. No saves, pairing,
// microphone capture, firmware writes or vendor browser initialization.
internal static class DeviceCenterStyleSelfTest
{
    internal static void Run(string directory,string? only=null) {
        Directory.CreateDirectory(directory);Exception? failure=null;
        using var loop=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new(-30000,-30000),Size=new(1,1)};
        loop.Shown+=async(_,_)=>{try{await CaptureAll();}catch(Exception ex){failure=ex;}finally{loop.Close();}};
        Application.Run(loop);if(failure is not null)throw new InvalidOperationException("Device Center style validation failed",failure);
        Console.WriteLine("DEVICE_CENTER_STYLE_OK all child windows and nested tabs; 100/150/200 percent default and minimum layouts, text and controls checked; isolated offline profile, no settings saved or hardware written");

        async Task CaptureAll() {
            var store=new DeviceRegistryStore(Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"style-fixtures","devices.json"));store.Migrate(null,false,false);
            var tab=DeviceRegistryStore.Create(HardwareKind.Tab5,"书桌 TAB5","001122334455");var esp=DeviceRegistryStore.Create(HardwareKind.Esp8266,"桌面小屏",null);store.Add(tab);store.Add(esp);
            using var tab5=new Tab5Service();tab5.UsbDevices=()=>[];
            using var voice=new Tab5VoiceHost(new Tab5VoiceSettings());
            var weather=new MigratedWeather.WeatherMonitor();
            var display=new Tab5DisplaySettings("12345678",75,35,false,-1,true,15,5,true,[1,1,1,1,1,1,1,0],[0,1,2,3,4,5,6,7]);
            var notifications=new BridgeNotifications();notifications.Evaluate("completion","fixture","任务完成",DateTimeOffset.Now);
            var sources=new[]{"天气","股票","Codex 额度","Claude 额度","DeepSeek 额度","智谱额度","通义千问额度","Kimi 额度"};
            using var updates=new UpdateService(new UpdateSelfTest.Handler{Reply=(_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("[]")})});
            UpdateDevice[] devices=[new("bridge","电脑端 AI-bot","0.6.3","运行中",true,"bridge","安装程序"),new("tab5","M5Stack TAB5","0.2.156-ui","在线",true,"upgrade-tab5","在设备上确认")];
            var pages=new List<(string Name,Func<Form> Create,Action<Form>? Prepare)> {
                ("devices",Center,f=>((DeviceCenterForm)f).ShowPage("devices")),
                ("tab-wifi-bridge-pending",()=>CenterWith(new(true,"在线","USB","0.2.204-ui",Usb:"已连接",Wifi:"已联网 · 桥接未通",Bluetooth:"已连接")),null),
                ("tab-wifi-bridge-ready",()=>CenterWith(new(true,"在线","Wi-Fi","0.2.204-ui",Usb:"未连接",Wifi:"已联网 · 桥接正常",Bluetooth:"未连接")),null),
                ("tab-wifi-offline",()=>CenterWith(new(true,"在线","USB","0.2.204-ui",Usb:"已连接",Wifi:"未联网",Bluetooth:"已连接")),null),
                ("tab-wifi-unknown",()=>CenterWith(new(false,"离线","无","0.2.204-ui",Usb:"未连接",Wifi:"等待设备回报",Bluetooth:"未连接")),null),
                ("esp-usb-interrupted",()=>CenterWith(new(true,"在线","Wi-Fi","0.5.1",Usb:"COM7 · 连接中断",Wifi:"已连接")),f=>((DeviceCenterForm)f).SelectDevice(esp.Id)),
                ("esp-usb-missing",()=>CenterWith(new(true,"在线","Wi-Fi","0.5.1",Usb:"未找到已绑定的 USB 设备",Wifi:"已连接")),f=>((DeviceCenterForm)f).SelectDevice(esp.Id)),
                ("esp-usb-connected",()=>CenterWith(new(true,"在线","USB","0.5.1",Usb:"已连接",Wifi:"待命")),f=>((DeviceCenterForm)f).SelectDevice(esp.Id)),
                ("tab-usb-connecting",()=>CenterWith(new(true,"在线","Wi-Fi","0.2.156-ui",Usb:"正在连接 COM9",Wifi:"已连接",Bluetooth:"未连接")),f=>((DeviceCenterForm)f).SelectDevice(tab.Id)),
                ("accounts",Center,f=>((DeviceCenterForm)f).ShowPage("accounts")),
                ("bridge-settings",Center,f=>((DeviceCenterForm)f).ShowPage("bridge-settings")),
                ("add-device",()=>new AddDeviceForm(new(1,true,false,false,[]),preview:true),null),
                ("add-device-complete",()=>new AddDeviceForm(store.Snapshot,preview:true),null),
                ("tab5-data",()=>new DeviceDataForm(store,tab.Id),null),
                ("esp-data",()=>new DeviceDataForm(store,esp.Id),null),
                ("display",()=>new Tab5DisplaySettingsForm((_,_)=>Task.FromResult(display)),null),
                ("esp-display",()=>new CycleSettingsForm(()=>Task.FromException<UsbDeviceInfo>(new IOException("USB 未连接")),_=>false),null),
                ("appearance",()=>new DeviceAppearanceForm("robot",_=>{},_=>{},_=>{},_=>{}),null),
                ("esp-connection",()=>new LegacyDeviceSettingsForm(view:()=>new(true,"在线","USB","0.5.1",Usb:"已连接",Wifi:"待命",Transport:"USB")),null),
                ("birthday",()=>new BirthdaySettingsForm(preview:true),null),
                ("task-pins",()=>new Tab5TaskPinsForm([new(Guid.NewGuid().ToString(),"检查任务回复与待处理操作","AI-bot",0)]),null),
                ("voice",()=>voice.CreateSettings(preview:true),null),
                ("voice-advanced",()=>voice.CreateSettings(preview:true),f=>((Button)f.Controls.Find("voice-troubleshoot",true).Single()).PerformClick()),
                ("model-accounts",()=>new DomesticQuotaAuthForm(null!,initializeBrowser:false),null),
                ("weather",()=>new MigratedWeather.WeatherSettingsForm(weather),null),
                ("stocks",()=>new SettingsForm(includeDeviceSettings:false,quotes:[]),null),
                ("quota-history",()=>new QuotaTrendForm(),null),
                ("status",()=>new BridgeStatusForm(()=>new([new("M5Stack TAB5","在线","Wi-Fi","0.2.156-ui"),new("ESP8266 小屏","在线","USB","0.5.1")],sources.Select(s=>new BridgeDataStatus(s,"数据可用","10-09 23:00:00")).ToArray(),2,null)),null),
                ("updates",()=>new UpdateCenterForm(()=>devices,(_,_)=>Task.CompletedTask,updates),null),
                ("art-gallery",()=>new GalleryPackForm(updates),null),
                ("configuration-backup",()=>new ConfigurationBackupForm(()=>{}),null),
                ("notifications",()=>new NotificationSettingsForm(notifications),null),
                ("about",()=>new AboutForm(),null),
                ("pet-gallery",()=>new PetGalleryForm(_=>{},load:false),null),
                ("esp-flash",()=>new FirmwareFlashForm(preview:true),null),
                ("tab5-install",()=>new Tab5InstallForm(tab5,preview:true),null)
            };
            foreach(int index in Enumerable.Range(0,5)) {int selected=index;pages.Add(("tab5-connection-"+index,()=>new Tab5ConnectionForm(tab5,loadNetworks:false),f=>Descendants(f).OfType<TabControl>().Single().SelectedIndex=selected));}
            var pairingStore=new Tab5PairingStore(Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"style-fixtures","remote-pair.dat"));
            var pairing=pairingStore.Pair("001122aabbcc",@"USB\OFFLINE-STYLE");
            using var remoteTab5=new Tab5Service(pairingStore);remoteTab5.UsbDevices=()=>[];
            foreach(string state in new[]{"ready","legacy","reported","reported-usb","verified"}) {
                string fixture=state;
                pages.Add(("tab5-upgrade-"+state,()=> {
                    var snapshot=new StatusSnapshot(1,"12:00",DateTimeOffset.UtcNow.ToUnixTimeSeconds(),0,DateTimeOffset.UtcNow,new("idle",null),new("idle",null));
                    remoteTab5.Publish(snapshot);remoteTab5.ObserveFirmware(pairing.DeviceId,fixture=="legacy"?"0.2.188-ui":"0.2.190-ui");
                    string nonce=Guid.NewGuid().ToString("N");remoteTab5.Respond(pairing.DeviceId,nonce,Tab5Protocol.Proof(Convert.FromBase64String(pairing.Key),$"GET|{pairing.DeviceId}|{nonce}"));
                    var image=new Tab5OtaPackage(new byte[1024],new string('a',64),fixture=="ready"?"0.2.191-ui":"0.2.190-ui",new string('b',32),"## 固件升级\n- 支持在电脑端开始升级\n## 兼容\n- 保留配对、图库和自动轮播");
                    remoteTab5.OfferOta(image);
                    typeof(Tab5Service).GetField("_otaTransferDiagnostic",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!
                        .SetValue(remoteTab5,fixture=="reported-usb"?"USB；已提供：6686880/6686880；等待设备校验":fixture is "reported" or "verified"?"Wi-Fi 分块压缩；镜像：7175664；实际传输：4565309/4565309；耗时毫秒：13407；等待设备校验":"尚无升级传输");
                    if(fixture=="verified")remoteTab5.RecordVerifiedBoot(JsonSerializer.SerializeToElement(new{type="tab5_ota_diagnostic",firmware=image.Version,elfSha256=image.ElfSha256,partition="ota_0",address=0x20000,state=2,stateError=0}),image,pairing.DeviceId);
                    return new Tab5ConnectionForm(remoteTab5,loadNetworks:false);
                },f=>Descendants(f).OfType<TabControl>().Single().SelectedIndex=2));
            }
            foreach(bool detail in new[]{false,true})pages.Add(("tab5-speed-"+(detail?"details":"summary"),()=>new Tab5ConnectionForm(tab5,loadNetworks:false),f=> {
                Descendants(f).OfType<TabControl>().Single().SelectedIndex=4;
                string sample="complete;gallery,Wi-Fi,8,2199663,7876,6900,1800,42,0;gallery_rpc,10,5800,120,5,8;gallery_io,2199663,2199663,200,900,80,8,4194304,128";
                ((TextBox)f.Controls.Find("benchmark-result",true).Single()).Text=detail?Tab5Service.FormatBenchmark(sample):Tab5Service.FormatBenchmarkOverview(sample);
                if(detail)Descendants(f).OfType<LinkLabel>().Single().Text="收起详细数据";
            }));
            foreach(string component in new[]{"bridge","tab5","esp8266"})pages.Add(("release-notes-"+component,()=>new UpdateNotesForm(new Dictionary<string,ComponentUpdate>(),devices,component),null));
            if(only is not null){var selected=only.Split(',').ToHashSet(StringComparer.Ordinal);pages.RemoveAll(p=>!selected.Contains(p.Name));if(pages.Count!=selected.Count)throw new ArgumentException("Unknown page filter");}
            var issues=new HashSet<string>();var captures=new List<object>();
            foreach(var page in pages)foreach(float scale in new[]{1f,1.5f,1.75f,2f})foreach(bool minimum in new[]{false,true}) {
                using var form=page.Create();form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.ShowInTaskbar=false;form.Show();page.Prepare?.Invoke(form);
                if(page.Name.StartsWith("tab5-upgrade-",StringComparison.Ordinal)) {
                    var status=Descendants(form).OfType<Tab5UpgradeStatusView>().Single();var expected=remoteTab5.UpgradeStatusText;
                    for(int update=0;update<8;update++){status.SetStatus(new("未选择固件","","选择固件后可查看版本和更新内容。","",""));form.PerformLayout();status.SetStatus(expected);form.PerformLayout();}
                }
                if(minimum)form.Size=form.MinimumSize;if(scale!=1)form.Scale(new SizeF(scale,scale));form.PerformLayout();await Task.Delay(30);Application.DoEvents();
                string name=$"{page.Name}-{scale:0.0#}-{(minimum?"minimum":"normal")}";
                Check(form.BackColor==Color.White,"Non-white window: "+page.Name);
                if(page.Name=="esp-connection") {
                    var saver=form.Controls.Find("esp-screensaver-minutes",true).Single();
                    var footer=((Button)form.AcceptButton!).Parent!;
                    var saverBounds=form.RectangleToClient(saver.RectangleToScreen(saver.ClientRectangle));
                    var footerBounds=form.RectangleToClient(footer.RectangleToScreen(footer.ClientRectangle));
                    Check(saverBounds.Bottom<=footerBounds.Top,"Connection settings footer overlaps screen-saver input: "+name);
                    Check(!Descendants(form).OfType<ScrollableControl>().Any(c=>c.AutoScroll),"Connection settings unexpectedly require scrolling: "+name);
                }
                if(page.Name.StartsWith("tab5-upgrade-",StringComparison.Ordinal)||page.Name.StartsWith("tab5-speed-",StringComparison.Ordinal)) {
                    var selectedTab=Descendants(form).OfType<TabControl>().Single().SelectedTab!;
                    var viewport=selectedTab.Controls.OfType<ScrollableControl>().Single();
                    if(viewport.HorizontalScroll.Visible) {
                        Console.WriteLine($"UPGRADE_WIDTH {name} viewport={viewport.ClientSize} pad={viewport.Padding} display={viewport.DisplayRectangle} scrollMin={viewport.AutoScrollMinSize} scrollMargin={viewport.AutoScrollMargin} minimum={viewport.MinimumSize}");
                        foreach(Control child in viewport.Controls)Console.WriteLine($"UPGRADE_CHILD {child.GetType().Name} text={child.Text} bounds={child.Bounds} margin={child.Margin} min={child.MinimumSize} max={child.MaximumSize}");
                    }
                    Check(!viewport.HorizontalScroll.Visible,"Upgrade page requires horizontal scrolling: "+name);
                    if(page.Name.StartsWith("tab5-upgrade-",StringComparison.Ordinal)) {
                        var status=Descendants(form).OfType<Tab5UpgradeStatusView>().Single();
                        Check(status.Height<=status.GetPreferredSize(new(status.Width,0)).Height+2,"Upgrade status stretched after updates: "+name);
                        Check(Descendants(form).OfType<Tab5ReleaseNotes>().Single().Height>=32,"Release notes squeezed out by status: "+name);
                        var transfer=form.Controls.Find("upgrade-transfer",true).Single();var progress=form.Controls.Find("upgrade-progress",true).Single();
                        if(page.Name=="tab5-upgrade-reported-usb")Check(!transfer.Visible&&progress.Text=="已通过 USB 传输 · 6.38 MiB","USB channel detached from transfer result: "+name);
                        if(transfer.Visible&&progress.Visible)Check(progress.Top-transfer.Bottom<20*scale*form.DeviceDpi/96f,"Transfer progress separated from its caption: "+name);
                    }
                }
                if(scale==1)Check(Math.Abs(form.Font.SizeInPoints-9.5)<.01,"Body typography differs: "+page.Name);
                foreach(Control control in Descendants(form).Where(c=>c.Visible)) {
                    if(control is Button button&&button.GetType()==typeof(Button)) {
                        Check(button.FlatStyle==FlatStyle.Flat,"Button style differs: "+page.Name+" "+button.Text);
                        Check(TextRenderer.MeasureText(button.Text,button.Font).Width<=button.ClientSize.Width+2,"Button text clipped: "+name+" "+button.Text);
                    }
                    if(control is Label label&&label.AutoSize)Check(label.Height+2>=label.GetPreferredSize(new(label.Width,0)).Height,"Label text clipped: "+name+" "+label.Text);
                    if(control is Label transport&&transport.Name.StartsWith("transport-",StringComparison.Ordinal)) {
                        var line=TextRenderer.MeasureText(transport.Text,transport.Font,Size.Empty,TextFormatFlags.SingleLine);
                        bool wrappedWifi=!transport.AutoEllipsis;
                        var required=wrappedWifi?TextRenderer.MeasureText(transport.Text,transport.Font,new Size(transport.ClientSize.Width,int.MaxValue),TextFormatFlags.WordBreak):line;
                        Check(!transport.AutoSize&&transport.Height>=required.Height,"Transport text clipped vertically: "+name+" "+transport.Text);
                        if(page.Name=="esp-usb-interrupted")Check(transport.Width>=line.Width,"Reported interruption text truncated: "+name+" "+transport.Text);
                    }
                    if(control is not (Label or Button or ComboBox or TextBox or NumericUpDown or CheckBox or ListView or CheckedListBox))continue;
                    var bounds=control.RectangleToScreen(control.ClientRectangle);bool scrolling=false;
                    for(Control? parent=control.Parent;parent is not null;parent=parent.Parent) {
                        scrolling|=parent is ScrollableControl{AutoScroll:true};var clip=parent.RectangleToScreen(parent.ClientRectangle);clip.Inflate(2,2);
                        Check(bounds.Left>=clip.Left&&bounds.Right<=clip.Right,"Horizontal clipping: "+name+" "+control.Text+" by "+parent.GetType().Name);
                        if(!scrolling)Check(bounds.Top>=clip.Top&&bounds.Bottom<=clip.Bottom,"Vertical clipping: "+name+" "+control.Text+" by "+parent.GetType().Name);
                    }
                }
                using var image=new Bitmap(form.Width,form.Height);form.DrawToBitmap(image,new(Point.Empty,form.Size));string file=name+".png";image.Save(Path.Combine(directory,file));captures.Add(new{page=page.Name,scale,minimum,file,width=form.Width,height=form.Height});form.Close();
            }
            File.WriteAllText(Path.Combine(directory,"report.json"),JsonSerializer.Serialize(new{pages=pages.Count,captures,issues},new JsonSerializerOptions{WriteIndented=true}));
            if(issues.Count>0)throw new InvalidOperationException(string.Join("\n",issues));
            void Check(bool ok,string issue){if(!ok)issues.Add(issue);}
            Form Center()=>new DeviceCenterForm(store,_=>new(true,"在线","Wi-Fi","0.2.156-ui",Usb:"待命",Wifi:"已连接",Bluetooth:"待命",Transport:"Wi-Fi"),(_,_)=>{},(_,_)=>Task.CompletedTask,()=>{},_=>{});
            Form CenterWith(DeviceView view)=>new DeviceCenterForm(store,_=>view,(_,_)=>{},(_,_)=>Task.CompletedTask,()=>{},_=>{});
        }
    }
    private static IEnumerable<Control> Descendants(Control root){foreach(Control control in root.Controls){yield return control;foreach(var child in Descendants(control))yield return child;}}
}
