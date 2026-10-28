using System.IO.Ports;
using System.Text.Json;

namespace AIBotBridge;

internal sealed class AddDeviceForm : Form
{
    private readonly CancellationTokenSource _stop=new();
    private bool _busy,_disposed;
    internal RegisteredDevice? Added {get;private set;}
    internal AddDeviceForm(DeviceRegistry registry,bool preview=false,Func<IReadOnlyList<FlashUsbDevice>>? discover=null) {
        SuspendLayout();
        Text="添加设备";Font=SettingsWindow.BodyFont();AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        ClientSize=new(560,400);MinimumSize=new(480,400);StartPosition=FormStartPosition.CenterParent;
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,AutoScroll=true,WrapContents=false,Padding=new Padding(24)};Controls.Add(panel);SettingsWindow.FitFlow(panel);
        var kind=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Label"};
        var choices=new[]{new Choice(HardwareKind.Tab5,"M5Stack TAB5"),new Choice(HardwareKind.Esp8266,"ESP8266 小屏")}.Where(c=>!registry.Devices.Any(d=>d.Kind==c.Kind)).ToArray();
        if(choices.Length==0) {
            kind.Dispose();Controls.Remove(panel);panel.Dispose();
            MinimumSize=new(400,250);ClientSize=new(420,230);BackColor=Color.White;MinimizeBox=false;MaximizeBox=false;
            var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(20,16,20,16),Margin=Padding.Empty};
            root.ColumnStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));
            var content=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,AutoScroll=true,WrapContents=false,Margin=Padding.Empty,Name="add-device-content"};
            SettingsWindow.FitFlow(content);root.Controls.Add(content,0,0);
            content.Controls.Add(new Label{Text="支持的设备均已添加",AutoSize=true,Font=new Font(Font,FontStyle.Bold),Margin=new Padding(0,0,0,12)});
            foreach(var device in registry.Devices)content.Controls.Add(new Label{Text=DeviceRegistryStore.Model(device.Kind)+" · 已添加"+(device.Enabled?"":"（已停用）"),AutoSize=true,Margin=new Padding(0,0,0,8)});
            content.Controls.Add(new Label{Text="每种型号一台；更换设备请先移除已有设备。",AutoSize=true,ForeColor=Color.DimGray,Margin=new Padding(0,8,0,0)});
            var footer=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Margin=new Padding(0,12,0,0),Name="add-device-actions"};
            var close=DeviceCenterForm.Button("关闭",()=>DialogResult=DialogResult.Cancel);close.Margin=Padding.Empty;footer.Controls.Add(close);root.Controls.Add(footer,0,1);Controls.Add(root);
            AcceptButton=close;CancelButton=close;SettingsWindow.FitScreen(this);ResumeLayout(true);return;
        }
        Controls.Remove(panel);panel.Padding=panel.Margin=Padding.Empty;
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(24)};
        layout.ColumnStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.AutoSize));layout.RowStyles.Add(new(SizeType.AutoSize));
        Controls.Add(layout);layout.Controls.Add(panel,0,0);
        panel.Controls.Add(new Label{Text="添加设备",AutoSize=true,Font=SettingsWindow.TitleFont()});
        panel.Controls.Add(new Label{Text="用 USB 数据线连接设备。",AutoSize=true,ForeColor=SettingsWindow.Muted,Margin=new(0,4,0,16)});
        kind.Items.AddRange(choices.Cast<object>().ToArray());kind.SelectedIndex=0;
        var fields=new TableLayoutPanel{AutoSize=true,ColumnCount=2,RowCount=0,Margin=Padding.Empty};
        fields.ColumnStyles.Add(new(SizeType.AutoSize));fields.ColumnStyles.Add(new(SizeType.Percent,100));panel.Controls.Add(fields);
        void Field(string title,Control input){int row=fields.RowCount++;fields.RowStyles.Add(new(SizeType.AutoSize));fields.Controls.Add(new Label{Text=title,AutoSize=true,Anchor=AnchorStyles.Left,Margin=new(0,8,16,8)},0,row);input.Anchor=AnchorStyles.Left|AnchorStyles.Right;input.Margin=new(0,6,0,6);fields.Controls.Add(input,1,row);}
        Field("设备型号",kind);
        var ports=new ComboBox{Name="add-device-ports",DropDownStyle=ComboBoxStyle.DropDownList,DisplayMember="Name",Anchor=AnchorStyles.Left|AnchorStyles.Right,Margin=Padding.Empty};
        var portRow=new TableLayoutPanel{AutoSize=true,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
        portRow.ColumnStyles.Add(new(SizeType.Percent,100));portRow.ColumnStyles.Add(new(SizeType.AutoSize));portRow.RowStyles.Add(new(SizeType.AutoSize));portRow.Controls.Add(ports,0,0);Field("USB 设备",portRow);
        var name=new TextBox{Text=DeviceRegistryStore.Model(((Choice)kind.SelectedItem!).Kind),MaxLength=40};Field("设备名称",name);
        var status=new Label{Name="add-device-status",AutoSize=true,Dock=DockStyle.Fill,ForeColor=SettingsWindow.Muted,Margin=new(0,12,0,8)};
        var add=DeviceCenterForm.Button("验证并添加",()=>{},true);add.Name="verify-add-device";
        void Refresh(){
            if(_busy)return;
            string? selected=(ports.SelectedItem as FlashUsbDevice)?.Identity;
            try{
                var devices=(discover?.Invoke()??(preview?Array.Empty<FlashUsbDevice>():FlashDeviceDiscovery.Read()))
                    .Where(d=>((Choice)kind.SelectedItem!).Kind==HardwareKind.Tab5?d.Identity.Contains("VID_303A",StringComparison.OrdinalIgnoreCase):!d.Identity.Contains("VID_303A",StringComparison.OrdinalIgnoreCase)).ToArray();
                ports.DataSource=devices;
                int previous=Array.FindIndex(devices,d=>d.Identity.Equals(selected,StringComparison.OrdinalIgnoreCase));
                ports.SelectedIndex=previous>=0?previous:selected is null&&devices.Length==1?0:-1;
                status.Text=devices.Length==0?"未发现 USB 设备，请检查数据线后刷新。":
                    ports.SelectedItem is null?"请选择 USB 设备；不确定时拔插后刷新。":"已发现 USB 设备。";
            }catch(Exception ex){ports.DataSource=Array.Empty<FlashUsbDevice>();status.Text="扫描失败，请重试："+ex.Message;}
            add.Enabled=ports.SelectedItem is FlashUsbDevice;
        }
        var refresh=DeviceCenterForm.Button("刷新设备",Refresh);refresh.Margin=new(8,0,0,0);portRow.Controls.Add(refresh,1,0);
        var install=DeviceCenterForm.Button("首次安装固件…",()=>{try{
            if(preview)return;
            if(((Choice)kind.SelectedItem!).Kind==HardwareKind.Tab5){using var service=new Tab5Service{AllowUnregisteredInstall=true};using var form=new Tab5InstallForm(service,returnToAdd:true);form.ShowDialog(this);}
            else {using var form=new FirmwareFlashForm(preferredPort:(ports.SelectedItem as FlashUsbDevice)?.Port,bridgeRunning:true,expectedUsbIdentity:(ports.SelectedItem as FlashUsbDevice)?.Identity);form.Text="新设备 · ESP8266 · 首次安装";form.ShowDialog(this);}
            Refresh();
        }catch(Exception ex){status.Text=ex.Message;}});install.Name="first-install-device";
        var routes=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Margin=new(0,16,0,0)};
        routes.ColumnStyles.Add(new(SizeType.Percent,50));routes.ColumnStyles.Add(new(SizeType.Percent,50));routes.RowStyles.Add(new(SizeType.AutoSize));routes.RowStyles.Add(new(SizeType.AutoSize));
        foreach(var (column,title,button) in new[]{(0,"已有 AI-bot",add),(1,"出厂设备",install)}){
            routes.Controls.Add(new Label{Text=title,AutoSize=true,Font=SettingsWindow.SectionFont(),Margin=new(0,0,12,6)},column,0);
            button.Anchor=AnchorStyles.Left;button.Margin=new(0,0,12,0);routes.Controls.Add(button,column,1);
        }
        layout.Controls.Add(routes,0,1);
        var guide=new LinkLabel{Text="安装指南与固件下载",AutoSize=true,Margin=new(0,12,0,8)};
        guide.LinkClicked+=(_,_)=>{try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://github.com/yaoyouzhong/AI-bot/blob/main/docs/INSTALL.zh.md"){UseShellExecute=true});}catch(Exception ex){status.Text="无法打开安装指南："+ex.Message;}};
        layout.Controls.Add(status,0,2);
        var actionsFooter=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
        actionsFooter.ColumnStyles.Add(new(SizeType.Percent,100));actionsFooter.ColumnStyles.Add(new(SizeType.AutoSize));actionsFooter.RowStyles.Add(new(SizeType.AutoSize));
        guide.Anchor=AnchorStyles.Left;guide.Margin=Padding.Empty;actionsFooter.Controls.Add(guide,0,0);
        var closeButton=DeviceCenterForm.Button("关闭",()=>Close());closeButton.Anchor=AnchorStyles.Right;actionsFooter.Controls.Add(closeButton,1,0);layout.Controls.Add(actionsFooter,0,3);CancelButton=closeButton;
        ports.SelectedIndexChanged+=(_,_)=>{add.Enabled=!_busy&&ports.SelectedItem is FlashUsbDevice;if(add.Enabled)status.Text="已选择 USB 设备。";};
        kind.SelectedIndexChanged+=(_,_)=>{name.Text=DeviceRegistryStore.Model(((Choice)kind.SelectedItem!).Kind);install.Text=((Choice)kind.SelectedItem!).Kind==HardwareKind.Tab5?"TAB5 首次安装…":"ESP8266 首次安装…";Refresh();};
        add.Click+=async(_,_)=> {
            if(_busy||preview)return;var selected=(Choice)kind.SelectedItem!;
            if(registry.Devices.Any(d=>d.Kind==selected.Kind)){status.Text="此型号已添加，请在设备中心管理现有设备。";return;}
            if(string.IsNullOrWhiteSpace(name.Text)){status.Text="请输入设备名称。";return;}
            if(ports.SelectedItem is not FlashUsbDevice device){status.Text="未找到 USB 设备，请连接后刷新。";return;}
            _busy=true;add.Enabled=refresh.Enabled=kind.Enabled=ports.Enabled=install.Enabled=false;status.Text="正在验证设备…";
            using var timeout=CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);timeout.CancelAfter(TimeSpan.FromSeconds(20));
            try {
                string? id=null,espId=null;
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
                else espId=(await DeviceProbe.VerifyEspAsync(device,timeout.Token)).DeviceId;
                Added=DeviceRegistryStore.Create(selected.Kind,name.Text,id) with {UsbIdentity=device.Identity,EspDeviceId=EspUsbRecovery.ValidId(espId)?espId:null};DialogResult=DialogResult.OK;
            }catch(Exception ex){status.Text=ex is OperationCanceledException?"验证已取消或超时，未添加设备。":ex.Message;}
            finally{_busy=false;if(!IsDisposed){refresh.Enabled=kind.Enabled=ports.Enabled=install.Enabled=true;add.Enabled=ports.SelectedItem is FlashUsbDevice;}}
        };
        install.Text=((Choice)kind.SelectedItem!).Kind==HardwareKind.Tab5?"TAB5 首次安装…":"ESP8266 首次安装…";
        FormClosing+=(_,e)=>{if(_busy){_stop.Cancel();e.Cancel=true;status.Text="正在取消验证，请稍候再关闭。";}};Refresh();SettingsWindow.FitScreen(this);ResumeLayout(true);
    }
    private sealed record Choice(HardwareKind Kind,string Label);
    protected override void Dispose(bool disposing){if(disposing&&!_disposed){_disposed=true;_stop.Cancel();_stop.Dispose();}base.Dispose(disposing);}
}

internal static class DeviceProbe
{
    internal static Task<UsbDeviceInfo> VerifyEspAsync(FlashUsbDevice device,CancellationToken token)=>Task.Run(async()=> {
        FlashDeviceSelection.RequireSame(device,FlashDeviceDiscovery.Read());
        using var port=new SerialPort(device.Port,460800){ReadTimeout=500,WriteTimeout=1500,NewLine="\n",DtrEnable=false,RtsEnable=false};
        port.Open();await Task.Delay(1200,token);port.DiscardInBuffer();
        uint request=(uint)Random.Shared.Next(1,int.MaxValue);
        port.WriteLine("@AIBOT "+JsonSerializer.Serialize(new{version=1,type="device_info_request",request_id=request}));
        long end=Environment.TickCount64+6000;
        while(Environment.TickCount64<end){token.ThrowIfCancellationRequested();try{var reply=UsbDeviceProtocol.ParseReply(port.ReadLine().Trim(),"device_info",request);if(reply is not null)return UsbDeviceProtocol.ReadInfo(reply.Value);}catch(TimeoutException){}}
        throw new IOException("未收到 ESP8266 小屏的有效协议回执，请检查连接与固件。");
    },token);
}
