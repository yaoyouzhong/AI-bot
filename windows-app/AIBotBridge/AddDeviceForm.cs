using System.IO.Ports;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class AddDeviceForm : Form
{
    private readonly CancellationTokenSource _stop=new();
    private bool _busy;
    internal RegisteredDevice? Added {get;private set;}
    internal AddDeviceForm(DeviceRegistry registry,bool preview=false) {
        Text="添加设备";Font=new Font("Microsoft YaHei UI",9F);AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        ClientSize=new(540,365);MinimumSize=new(480,350);StartPosition=FormStartPosition.CenterParent;
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,AutoScroll=true,WrapContents=false,Padding=new Padding(20)};Controls.Add(panel);SettingsWindow.FitFlow(panel);
        panel.Controls.Add(new Label{Text="连接 USB 数据线，验证设备后添加。",AutoSize=true,Font=new Font(Font.FontFamily,12,FontStyle.Bold)});
        var kind=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Label"};
        var choices=new[]{new Choice(HardwareKind.Tab5,"TAB5 平板"),new Choice(HardwareKind.Esp8266,"ESP8266 小屏")}.Where(c=>!registry.Devices.Any(d=>d.Kind==c.Kind)).ToArray();
        if(choices.Length==0)throw new InvalidOperationException("当前已添加两种设备，请管理已有设备。");
        kind.Items.AddRange(choices.Cast<object>().ToArray());kind.SelectedIndex=0;panel.Controls.Add(kind);
        var ports=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Name"};panel.Controls.Add(ports);
        var name=new TextBox{Text=DeviceRegistryStore.Model(((Choice)kind.SelectedItem!).Kind),MaxLength=40};panel.Controls.Add(name);
        var status=new Label{AutoSize=true,Text="USB 名称只用于发现；完成协议验证才会添加。",ForeColor=Color.DimGray};
        void Refresh(){try{if(!preview)ports.DataSource=FlashDeviceDiscovery.Read().Where(d=>((Choice)kind.SelectedItem!).Kind==HardwareKind.Tab5?d.Identity.Contains("VID_303A",StringComparison.OrdinalIgnoreCase):!d.Identity.Contains("VID_303A",StringComparison.OrdinalIgnoreCase)).ToArray();}catch(Exception ex){status.Text="读取设备列表失败："+ex.Message;}}
        var row=DeviceCenterForm.Row();var refresh=DeviceCenterForm.Button("刷新设备",Refresh);row.Controls.Add(refresh);
        var add=DeviceCenterForm.Button("验证并添加",()=>{},true);row.Controls.Add(add);panel.Controls.Add(row);
        var install=DeviceCenterForm.Button("首次安装固件…",()=>{try{
            if(((Choice)kind.SelectedItem!).Kind==HardwareKind.Tab5){using var service=new Tab5Service{AllowUnregisteredInstall=true};using var form=new Tab5InstallForm(service);form.ShowDialog(this);}
            else {using var form=new FirmwareFlashForm(preferredPort:(ports.SelectedItem as FlashUsbDevice)?.Port);form.Text="新设备 · ESP8266 · 首次安装";form.ShowDialog(this);}
            Refresh();
        }catch(Exception ex){status.Text=ex.Message;}});panel.Controls.Add(install);panel.Controls.Add(status);
        kind.SelectedIndexChanged+=(_,_)=>{name.Text=DeviceRegistryStore.Model(((Choice)kind.SelectedItem!).Kind);install.Text=((Choice)kind.SelectedItem!).Kind==HardwareKind.Tab5?"TAB5 首次安装…":"ESP8266 首次安装…";Refresh();};
        add.Click+=async(_,_)=> {
            if(_busy)return;var selected=(Choice)kind.SelectedItem!;
            if(registry.Devices.Any(d=>d.Kind==selected.Kind)){status.Text="此型号已添加，请在设备中心管理现有设备。";return;}
            if(string.IsNullOrWhiteSpace(name.Text)){status.Text="请输入设备名称。";return;}
            if(ports.SelectedItem is not FlashUsbDevice device){status.Text="未找到 USB 设备，请连接后刷新。";return;}
            _busy=true;add.Enabled=refresh.Enabled=kind.Enabled=ports.Enabled=install.Enabled=false;status.Text="正在验证设备…";
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);timeout.CancelAfter(TimeSpan.FromSeconds(20));
            try {
                string? id=null;
                if(selected.Kind==HardwareKind.Tab5){
                    using var service=new Tab5Service();
                    try{await service.PairUsbAsync(device,timeout.Token);}
                    catch(Tab5IdentityConflictException conflict){
                        timeout.CancelAfter(Timeout.InfiniteTimeSpan);
                        if(MessageBox.Show(this,"检测到保留的 TAB5 配对资料。为当前新设备建立配对？\n原配对资料将加密备份，旧设备需重新配对。","替换保留的配对",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK)return;
                        timeout.CancelAfter(TimeSpan.FromSeconds(20));await service.PairUsbAsync(device,timeout.Token,conflict.CandidateId);
                    }
                    id=service.PairedId;
                }
                else await DeviceProbe.VerifyEspAsync(device,timeout.Token);
                Added=DeviceRegistryStore.Create(selected.Kind,name.Text,id) with {UsbIdentity=device.Identity};DialogResult=DialogResult.OK;
            }catch(Exception ex){status.Text=ex is OperationCanceledException?"验证已取消或超时，未添加设备。":ex.Message;}
            finally{_busy=false;if(!IsDisposed)add.Enabled=refresh.Enabled=kind.Enabled=ports.Enabled=install.Enabled=true;}
        };
        install.Text=((Choice)kind.SelectedItem!).Kind==HardwareKind.Tab5?"TAB5 首次安装…":"ESP8266 首次安装…";
        FormClosing+=(_,e)=>{if(_busy){_stop.Cancel();e.Cancel=true;status.Text="正在取消验证，请稍候再关闭。";}};Refresh();SettingsWindow.FitScreen(this);
    }
    private sealed record Choice(HardwareKind Kind,string Label);
    protected override void Dispose(bool disposing){if(disposing){_stop.Cancel();_stop.Dispose();}base.Dispose(disposing);}
}

internal static class DeviceProbe
{
    internal static Task VerifyEspAsync(FlashUsbDevice device,CancellationToken token)=>Task.Run(async()=> {
        FlashDeviceSelection.RequireSame(device,FlashDeviceDiscovery.Read());
        using var port=new SerialPort(device.Port,460800){ReadTimeout=500,WriteTimeout=1500,NewLine="\n",DtrEnable=false,RtsEnable=false};
        port.Open();await Task.Delay(1200,token);port.DiscardInBuffer();
        uint request=(uint)Random.Shared.Next(1,int.MaxValue);
        port.WriteLine("@AIBOT "+JsonSerializer.Serialize(new{version=1,type="device_info_request",request_id=request}));
        long end=Environment.TickCount64+6000;
        while(Environment.TickCount64<end){token.ThrowIfCancellationRequested();try{var reply=UsbDeviceProtocol.ParseReply(port.ReadLine().Trim(),"device_info",request);if(reply is not null){_=UsbDeviceProtocol.ReadInfo(reply.Value);return;}}catch(TimeoutException){}}
        throw new IOException("未收到 ESP8266 小屏的有效协议回执，请检查连接与固件。");
    },token);
}
