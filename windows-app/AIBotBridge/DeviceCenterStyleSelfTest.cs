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
            foreach(string component in new[]{"bridge","tab5","esp8266"})pages.Add(("release-notes-"+component,()=>new UpdateNotesForm(new Dictionary<string,ComponentUpdate>(),devices,component),null));
            if(only is not null){var selected=only.Split(',').ToHashSet(StringComparer.Ordinal);pages.RemoveAll(p=>!selected.Contains(p.Name));if(pages.Count!=selected.Count)throw new ArgumentException("Unknown page filter");}
            var issues=new HashSet<string>();var captures=new List<object>();
            foreach(var page in pages)foreach(float scale in new[]{1f,1.5f,2f})foreach(bool minimum in new[]{false,true}) {
                using var form=page.Create();form.StartPosition=FormStartPosition.Manual;form.Location=new(-30000,-30000);form.ShowInTaskbar=false;form.Show();page.Prepare?.Invoke(form);
                if(minimum)form.Size=form.MinimumSize;if(scale!=1)form.Scale(new SizeF(scale,scale));form.PerformLayout();await Task.Delay(30);Application.DoEvents();
                string name=$"{page.Name}-{scale:0.0}-{(minimum?"minimum":"normal")}";
                Check(form.BackColor==Color.White,"Non-white window: "+page.Name);
                if(scale==1)Check(Math.Abs(form.Font.SizeInPoints-9.5)<.01,"Body typography differs: "+page.Name);
                foreach(Control control in Descendants(form).Where(c=>c.Visible)) {
                    if(control is Button button&&button.GetType()==typeof(Button)) {
                        Check(button.FlatStyle==FlatStyle.Flat,"Button style differs: "+page.Name+" "+button.Text);
                        Check(TextRenderer.MeasureText(button.Text,button.Font).Width<=button.ClientSize.Width+2,"Button text clipped: "+name+" "+button.Text);
                    }
                    if(control is Label label&&label.AutoSize)Check(label.Height+2>=label.GetPreferredSize(new(label.Width,0)).Height,"Label text clipped: "+name+" "+label.Text);
                    if(control is Label transport&&transport.Name.StartsWith("transport-",StringComparison.Ordinal)) {
                        var line=TextRenderer.MeasureText(transport.Text,transport.Font,Size.Empty,TextFormatFlags.SingleLine);
                        Check(transport.AutoEllipsis&&!transport.AutoSize&&transport.Height>=line.Height,"Transport row wraps/clips vertically: "+name+" "+transport.Text);
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
