namespace AIBotBridge;

internal sealed record DeviceView(bool Online,string Status,string Connection,string Firmware,
    string? Usb=null,string? Wifi=null,string? Bluetooth=null,string? Transport=null);

internal sealed class DeviceCenterForm : Form
{
    private readonly DeviceRegistryStore _store;
    private readonly Func<RegisteredDevice,DeviceView> _view;
    private readonly Action<string,string> _action;
    private readonly Func<RegisteredDevice,bool,Task> _change;
    private readonly Action _add;
    private readonly Action<string> _common;
    private readonly Func<string,bool> _setting;
    private readonly TabControl _pages=new(){Dock=DockStyle.Fill,Padding=new(18,9)};
    private readonly ListBox _list=new(){Dock=DockStyle.Fill,DisplayMember="Name",IntegralHeight=false,BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=36};
    private readonly TableLayoutPanel _deviceLayout=new(){Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=Padding.Empty};
    private readonly FlowLayoutPanel _detail=Column();
    private readonly FlowLayoutPanel _accounts=Column();
    private readonly FlowLayoutPanel _bridge=Column();
    private readonly FlowLayoutPanel _legacy=Column();
    private readonly Label _state=new(){AutoSize=true};
    private readonly Label _firmware=new(){AutoSize=true,ForeColor=Color.DimGray};
    private readonly TableLayoutPanel _health=new(){AutoSize=true,ColumnCount=2,RowCount=1,Margin=new Padding(0,0,0,6)};
    private readonly Label _connection=new(){AutoSize=true,ForeColor=Color.DimGray};
    private readonly TableLayoutPanel _transport=new(){AutoSize=true,ColumnCount=3,RowCount=1,Margin=new Padding(0,4,0,8)};
    private readonly Label[] _transportLabels=[new(),new(),new()];
    private readonly System.Windows.Forms.Timer _timer=new(){Interval=1500};
    private readonly List<(Button Button,string Action)> _actions=[];
    private readonly ContextMenuStrip _manage=new();
    private readonly Button _addButton;
    private bool _reloading;
    private RegisteredDevice? Selected=>_list.SelectedItem as RegisteredDevice;

    internal DeviceCenterForm(DeviceRegistryStore store,Func<RegisteredDevice,DeviceView> view,Action<string,string> action,
        Func<RegisteredDevice,bool,Task> change,Action add,Action<string> common,Func<string,bool>? setting=null)
    {
        _store=store;_view=view;_action=action;_change=change;_add=add;_common=common;_setting=setting??(_=>false);
        _health.ColumnStyles.Add(new(SizeType.AutoSize));_health.ColumnStyles.Add(new(SizeType.Percent,100));_health.RowStyles.Add(new(SizeType.AutoSize));_state.Margin=new Padding(0,0,18,0);_firmware.Margin=Padding.Empty;_health.Controls.Add(_state,0,0);_health.Controls.Add(_firmware,1,0);
        for(int i=0;i<3;i++){_transport.ColumnStyles.Add(new(SizeType.Percent,100F/3));var label=_transportLabels[i];label.AutoSize=true;label.Dock=DockStyle.Fill;label.Margin=new Padding(0,0,8,0);_transport.Controls.Add(label,i,0);}
        _transport.RowStyles.Add(new(SizeType.AutoSize));
        Text="AI-bot · 设备中心";Font=new Font("Microsoft YaHei UI",9F);AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        ClientSize=new(840,600);MinimumSize=new(760,500);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.White;Padding=new Padding(12);
        var devicesPage=Page("我的设备","devices");Page("账号数据","accounts").Controls.Add(_accounts);Page("桥接设置","bridge-settings").Controls.Add(_bridge);
        var body=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Margin=Padding.Empty};
        body.ColumnStyles.Add(new(SizeType.Percent,100));body.RowStyles.Add(new(SizeType.Percent,100));body.RowStyles.Add(new(SizeType.AutoSize));
        var split=_deviceLayout;
        split.ColumnStyles.Add(new(SizeType.Absolute,184));split.ColumnStyles.Add(new(SizeType.Percent,100));split.RowStyles.Add(new(SizeType.Percent,100));
        var sidebar=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(8),BackColor=Color.FromArgb(243,246,249),Margin=new Padding(0,0,12,0)};
        sidebar.ColumnStyles.Add(new(SizeType.Percent,100));sidebar.RowStyles.Add(new(SizeType.Percent,100));sidebar.RowStyles.Add(new(SizeType.AutoSize));
        _list.BackColor=sidebar.BackColor;sidebar.Controls.Add(_list,0,0);
        _addButton=Button("＋ 添加",_add,true);_addButton.Name="add-device";_addButton.Dock=DockStyle.Fill;sidebar.Controls.Add(_addButton,0,1);
        split.Controls.Add(sidebar,0,0);split.Controls.Add(_detail,1,0);body.Controls.Add(split,0,0);
        _legacy.Dock=DockStyle.Top;_legacy.AutoSize=true;_legacy.AutoScroll=false;_legacy.Padding=new Padding(12,8,12,8);_legacy.Margin=new Padding(0,10,0,0);_legacy.BackColor=Color.FromArgb(242,246,252);body.Controls.Add(_legacy,0,1);
        devicesPage.Controls.Add(body);Controls.Add(_pages);
        _list.DrawItem+=DrawDevice;_list.SizeChanged+=(_,_)=>FitDeviceNames();_list.FontChanged+=(_,_)=>FitDeviceNames();_list.SelectedIndexChanged+=(_,_)=>{if(!_reloading)Render();};
        _timer.Tick+=(_,_)=>RefreshStatus();_timer.Start();Reload();SettingsWindow.FitScreen(this);
    }
    private TabPage Page(string title,string name){var page=new TabPage(title){Name=name,Padding=new Padding(10),BackColor=Color.White};_pages.TabPages.Add(page);return page;}
    internal void ShowPage(string name){if(_pages.TabPages.ContainsKey(name))_pages.SelectedTab=_pages.TabPages[name];}
    internal void SelectDevice(string id){ShowPage("devices");for(int i=0;i<_list.Items.Count;i++)if(_list.Items[i] is RegisteredDevice d&&d.Id==id){_list.SelectedIndex=i;return;}}
    private void DrawDevice(object? sender,DrawItemEventArgs e) {
        if(e.Index<0||_list.Items[e.Index] is not RegisteredDevice d)return;
        bool selected=(e.State&DrawItemState.Selected)!=0;
        using var background=new SolidBrush(selected?Color.FromArgb(221,234,252):_list.BackColor);e.Graphics.FillRectangle(background,e.Bounds);
        var rect=Rectangle.Inflate(e.Bounds,-8,-7);
        TextRenderer.DrawText(e.Graphics,d.Name,e.Font,rect,!d.Enabled?Color.DimGray:selected?Color.FromArgb(24,81,163):Color.FromArgb(35,42,52),TextFormatFlags.WordBreak|TextFormatFlags.VerticalCenter);
        e.DrawFocusRectangle();
    }
    private void FitDeviceNames(){
        if(_list.Items.Count==0||_list.ClientSize.Width<40)return;
        using var graphics=_list.CreateGraphics();
        int preferred=_list.Items.Cast<RegisteredDevice>().Max(d=>TextRenderer.MeasureText(graphics,d.Name,_list.Font).Width)+64;
        int width=Math.Clamp(preferred,220,Math.Max(220,_deviceLayout.ClientSize.Width*35/100));if(_deviceLayout.ColumnStyles.Count>0&&_deviceLayout.ColumnStyles[0].Width!=width)_deviceLayout.ColumnStyles[0].Width=width;
        int height=_list.Items.Cast<RegisteredDevice>().Max(d=>TextRenderer.MeasureText(graphics,d.Name,_list.Font,new Size(_list.ClientSize.Width-16,int.MaxValue),TextFormatFlags.WordBreak).Height)+24;
        if(_list.ItemHeight!=height)_list.ItemHeight=height;
    }
    internal void Reload() {
        string? id=Selected?.Id;var devices=_store.Snapshot.Devices;
        _reloading=true;try{_list.DataSource=devices;_list.SelectedIndex=devices.Length==0?-1:Math.Max(0,Array.FindIndex(devices,d=>d.Id==id));}finally{_reloading=false;}
        FitDeviceNames();
        Render();RenderShared();RenderLegacy();
    }
    private void Render() {
        _detail.SuspendLayout();_detail.Controls.Remove(_health);_detail.Controls.Remove(_connection);_detail.Controls.Remove(_transport);Clear(_detail);_actions.Clear();_manage.Items.Clear();
        if(Selected is not { } d) {
            Heading(_detail,"还没有添加设备");Note(_detail,"先添加 M5Stack TAB5 或 ESP8266 小屏。");
            _detail.Controls.Add(Button("添加第一台设备",_add,true));
        } else {
            var heading=new TableLayoutPanel{AutoSize=true,ColumnCount=2,RowCount=1,Margin=new Padding(0,0,0,6)};
            heading.ColumnStyles.Add(new(SizeType.Percent,100));heading.ColumnStyles.Add(new(SizeType.AutoSize));heading.RowStyles.Add(new(SizeType.AutoSize));
            heading.Controls.Add(new Label{Text=d.Name,AutoSize=true,MaximumSize=new(400,0),Font=new Font(Font.FontFamily,12,FontStyle.Bold),Anchor=AnchorStyles.Left,Margin=new Padding(0,3,8,3)},0,0);
            var manage=Button("管理…",()=>{});manage.Click+=(_,_)=>_manage.Show(manage,new Point(0,manage.Height));heading.Controls.Add(manage,1,0);_detail.Controls.Add(heading);
            _manage.Items.Add("修改名称",null,(_,_)=>Rename(d));
            _manage.Items.Add(d.Enabled?"停用设备":"启用设备",null,async(_,_)=>await Change(d,false));
            if(d.Kind==HardwareKind.Esp8266){
                _manage.Items.Add(new ToolStripSeparator());
                foreach(var (label,key) in new[]{("固件升级","flash"),("设备信息","info"),("连接诊断","fallback"),("重置网络","reset")}){
                    var item=new ToolStripMenuItem(label){Tag=key};item.Click+=(_,_)=>_action(d.Id,key);_manage.Items.Add(item);
                }
            }
            _manage.Items.Add(new ToolStripSeparator());_manage.Items.Add("移除设备…",null,async(_,_)=>await Change(d,true));
            _connection.Margin=new Padding(0,0,0,12);_detail.Controls.Add(_health);_detail.Controls.Add(_transport);_detail.Controls.Add(_connection);
            var grid=ActionGrid(_detail);
            string[] actions=d.Kind==HardwareKind.Esp8266?["cycle","appearance","data","legacy-settings"]:["voice","birthday-settings","data","tab5"];
            foreach(string action in actions) {
                if(action=="data"){
                    Card(grid,"数据设置","采集内容与国产模型",()=>{using var dialog=new DeviceDataForm(_store,d.Id);if(dialog.ShowDialog(this)==DialogResult.OK){_action(d.Id,"data-changed");Reload();}});continue;
                }
                string title=action switch {"tab5"=>"连接升级","birthday-settings"=>"日历生日","legacy-settings"=>"连接设置","cycle"=>"显示设置","appearance"=>"外观设置",_=>DeviceCapabilities.Actions(d.Kind).Single(c=>c.Action==action).Label};
                var button=Card(grid,title,Description(action),()=>_action(d.Id,action));_actions.Add((button,action));
            }
            RefreshStatus();
        }
        _detail.ResumeLayout(true);
    }
    private void RenderLegacy() {
        Clear(_legacy);_legacy.Visible=_store.Snapshot.LegacyDecisionPending;
        if(!_store.Snapshot.LegacyDecisionPending)return;
        var row=Row();row.Margin=Padding.Empty;
        row.Controls.Add(new Label{Text="发现 ESP8266 旧配置",AutoSize=true,ForeColor=Color.DimGray,Margin=new Padding(0,7,12,7)});
        row.Controls.Add(Button("导入 ESP8266",()=>_action("","migrate-legacy")));row.Controls.Add(Button("不再提示",()=>_action("","dismiss-legacy")));_legacy.Controls.Add(row);
    }
    private void RenderShared() {
        Clear(_accounts);Heading(_accounts,"账号数据",12);Note(_accounts,"在电脑上配置一次，所有已添加设备共用。");
        var accounts=ActionGrid(_accounts);
        Card(accounts,"模型账号","登录与授权模型账号",()=>_common("authorize"));
        Card(accounts,"天气定位","城市、定位与天气来源",()=>_common("weather-settings"));
        Card(accounts,"自选股票","管理关注的股票",()=>_common("stocks-settings"));
        Card(accounts,"额度历史","查看 Codex 额度记录",()=>_common("quota-trend"));
        Clear(_bridge);Heading(_bridge,"桥接设置",12);Note(_bridge,"管理这台电脑上的桥接程序。");
        var bridge=ActionGrid(_bridge);
        Card(bridge,"开机启动",_setting("startup")?"已开启 · 点击关闭":"已关闭 · 点击开启",()=>_common("startup"));
        Card(bridge,"服务状态","连接状态与数据更新",()=>_common("status"));
        Card(bridge,"关于应用","版本、许可与项目信息",()=>_common("about"));
    }
    private async Task Change(RegisteredDevice d,bool remove) {
        if(remove&&MessageBox.Show(this,$"移除“{d.Name}”？\n保留配置、配对恢复资料、生日和备份，不清空硬件。","移除设备",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK)return;
        Enabled=false;try{await _change(d,remove);Reload();}catch(Exception ex){MessageBox.Show(this,ex.Message,"设备操作未完成");}finally{if(!IsDisposed)Enabled=true;}
    }
    private void Rename(RegisteredDevice d) {
        using var form=new Form{Text="修改设备名称",ClientSize=new(360,125),StartPosition=FormStartPosition.CenterParent,Font=Font,FormBorderStyle=FormBorderStyle.FixedDialog,MinimizeBox=false,MaximizeBox=false};
        var name=new TextBox{Text=d.Name,MaxLength=40,Dock=DockStyle.Top};var save=Button("保存",()=>{try{_store.Update(d with{Name=name.Text.Trim()});form.DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show(form,ex.Message);}},true);
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,Padding=new Padding(16)};name.Width=310;panel.Controls.Add(name);panel.Controls.Add(save);form.Controls.Add(panel);form.AcceptButton=save;SettingsWindow.FitScreen(form);
        if(form.ShowDialog(this)==DialogResult.OK){_action(d.Id,"renamed");Reload();}
    }
    private void RefreshStatus() {
        if(Selected is not { } d)return;var v=_view(d);
        string state=d.Enabled?v.Status:"已停用";if(v.Online&&v.Transport is not null)state+=" · "+v.Transport;if(_state.Text!=state)_state.Text=state;_state.ForeColor=d.Enabled&&v.Online?Color.FromArgb(24,123,72):Color.DimGray;
        string firmware="固件 "+v.Firmware;if(_firmware.Text!=firmware)_firmware.Text=firmware;
        bool structured=v.Usb is not null;_transport.Visible=structured;
        string?[] values=[v.Usb,v.Wifi,v.Bluetooth];string[] names=["USB","Wi-Fi","蓝牙"];
        for(int i=0;i<3;i++){var label=_transportLabels[i];string text=names[i]+" · "+(values[i]??"不支持");if(label.Text!=text)label.Text=text;label.ForeColor=d.Enabled&&values[i]=="已连接"?Color.FromArgb(24,123,72):Color.DimGray;}
        _transportLabels[2].Visible=d.Kind==HardwareKind.Tab5;_transport.ColumnStyles[2].Width=d.Kind==HardwareKind.Tab5?100F/3:0;
        _connection.Visible=!structured;string connection=v.Connection;if(_connection.Text!=connection)_connection.Text=connection;
        foreach(var (button,action) in _actions)button.Enabled=DeviceCapabilities.Allows(d,action)&&(!DeviceCapabilities.RequiresOnline(action)||v.Online);
        foreach(var item in _manage.Items.OfType<ToolStripMenuItem>().Where(i=>i.Tag is string))item.Enabled=DeviceCapabilities.Allows(d,(string)item.Tag!)&&(!DeviceCapabilities.RequiresOnline((string)item.Tag!)||v.Online);
    }
    private static string Description(string action)=>action switch {
        "tab5"=>"连接、Wi-Fi 与固件", "voice"=>"豆包语音与麦克风", "birthday-settings"=>"生日、农历与提醒",
        "legacy-settings"=>"连接方式与空闲屏保", "cycle"=>"亮度、页面与自动轮播", "mirror"=>"预览小屏当前画面",
        "flash"=>"选择固件并升级", "pet-gallery"=>"浏览桌宠素材", "appearance"=>"天气动画、桌宠与素材", _=>""
    };
    private static void Clear(Control panel){foreach(Control c in panel.Controls.Cast<Control>().ToArray())c.Dispose();panel.Controls.Clear();}
    private static FlowLayoutPanel Column(){var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(4),Margin=Padding.Empty,BackColor=Color.White};SettingsWindow.FitFlow(panel);return panel;}
    private void Heading(Control parent,string text,float size=14)=>parent.Controls.Add(new Label{Text=text,AutoSize=true,Font=new Font(Font.FontFamily,size,FontStyle.Bold),Margin=new Padding(0,4,0,10)});
    private static void Note(Control parent,string text)=>parent.Controls.Add(new Label{Text=text,AutoSize=true,ForeColor=Color.DimGray,Margin=new Padding(0,0,0,12)});
    private static TableLayoutPanel ActionGrid(Control parent) {
        var grid=new TableLayoutPanel{AutoSize=true,ColumnCount=2,Margin=new Padding(0,10,0,0)};
        grid.ColumnStyles.Add(new(SizeType.Percent,50));grid.ColumnStyles.Add(new(SizeType.Percent,50));parent.Controls.Add(grid);return grid;
    }
    private static Button Card(TableLayoutPanel grid,string title,string description,Action action) {
        int index=grid.Controls.Count;
        if(index%2==0){grid.RowCount++;grid.RowStyles.Add(new(SizeType.AutoSize));}
        var card=new DeviceActionButton(title,description){Dock=DockStyle.Fill,Margin=new Padding(index%2==0?0:5,0,index%2==0?5:0,10)};
        card.Click+=(_,_)=>action();grid.Controls.Add(card,index%2,index/2);return card;
    }
    internal static FlowLayoutPanel Row()=>new(){AutoSize=true,WrapContents=true,Margin=new Padding(0,4,0,8)};
    internal static Button Button(string text,Action click,bool primary=false) {var b=new Button{Text=text,AutoSize=true};SettingsWindow.StyleButton(b,primary);b.Click+=(_,_)=>click();return b;}
    protected override void Dispose(bool disposing){if(disposing){_timer.Stop();_timer.Dispose();_manage.Dispose();_health.Dispose();_connection.Dispose();_transport.Dispose();}base.Dispose(disposing);}
}
