namespace AIBotBridge;

internal sealed record DeviceView(bool Online,string Status,string Connection,string Firmware);

internal sealed class DeviceCenterForm : Form
{
    private readonly DeviceRegistryStore _store;
    private readonly Func<RegisteredDevice,DeviceView> _view;
    private readonly Action<string,string> _action;
    private readonly Func<RegisteredDevice,bool,Task> _change;
    private readonly Action _add;
    private readonly ListBox _list=new(){Dock=DockStyle.Fill,DisplayMember="Name",IntegralHeight=false};
    private readonly FlowLayoutPanel _detail=new(){Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(16)};
    private readonly TableLayoutPanel _body=new(){Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};
    private readonly Label _state=new(){AutoSize=true,ForeColor=Color.DimGray};
    private readonly System.Windows.Forms.Timer _timer=new(){Interval=1500};
    private readonly List<(Button Button,string Action)> _actions=[];
    private RegisteredDevice? Selected=>_list.SelectedItem as RegisteredDevice;
    internal DeviceCenterForm(DeviceRegistryStore store,Func<RegisteredDevice,DeviceView> view,Action<string,string> action,
        Func<RegisteredDevice,bool,Task> change,Action add,Action common)
    {
        _store=store;_view=view;_action=action;_change=change;_add=add;
        Text="AI-bot · 我的设备";Font=new Font("Microsoft YaHei UI",9F);AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        ClientSize=new(760,520);MinimumSize=new(600,440);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(246,248,250);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(12)};
        root.ColumnStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Percent,100));
        var top=Row();top.Controls.Add(Button("添加设备",()=>_add(),true));top.Controls.Add(Button("账号与数据源",common));root.Controls.Add(top,0,0);
        _body.ColumnStyles.Add(new(SizeType.Absolute,180));_body.ColumnStyles.Add(new(SizeType.Percent,100));_body.RowStyles.Add(new(SizeType.Percent,100));
        _body.Controls.Add(_list,0,0);_body.Controls.Add(_detail,1,0);_detail.BackColor=Color.White;root.Controls.Add(_body,0,1);Controls.Add(root);
        SettingsWindow.FitFlow(_detail);_list.SelectedIndexChanged+=(_,_)=>Render();
        _timer.Tick+=(_,_)=>RefreshStatus();_timer.Start();Reload();SettingsWindow.FitScreen(this);
    }
    internal void Reload() {
        string? id=Selected?.Id;var devices=_store.Snapshot.Devices;
        _list.DataSource=devices;_list.SelectedIndex=devices.Length==0?-1:Math.Max(0,Array.FindIndex(devices,d=>d.Id==id));
        _list.Visible=devices.Length>1;_body.ColumnStyles[0].Width=devices.Length>1?180:0;Render();
    }
    private void Render() {
        _detail.SuspendLayout();_detail.Controls.Remove(_state);foreach(Control c in _detail.Controls.Cast<Control>().ToArray())c.Dispose();_detail.Controls.Clear();_actions.Clear();
        var d=Selected;
        if(d is null) {
            AddText("连接你的设备",16,true);AddText("添加 TAB5 平板或 ESP8266 小屏，显示对应功能。",10);
            _detail.Controls.Add(Button("添加设备",_add,true));
            if(_store.Snapshot.LegacyDecisionPending) {
                AddText("发现原小屏配置。确认后可沿用原连接、轮播与屏保设置。",9);
                _detail.Controls.Add(Button("继续使用原小屏",()=>_action("","migrate-legacy")));
                _detail.Controls.Add(Button("暂不使用",()=>_action("","dismiss-legacy")));
            }
        } else {
            AddText(d.Name,14,true);AddText(DeviceRegistryStore.Model(d.Kind),9);_detail.Controls.Add(_state);
            var management=Row();management.Controls.Add(Button("修改名称",()=>Rename(d)));
            management.Controls.Add(Button(d.Enabled?"停用设备":"启用设备",async()=>await Change(d,false)));
            management.Controls.Add(Button("移除设备",async()=>await Change(d,true)));

            var functions=Row();
            foreach(var capability in DeviceCapabilities.Actions(d.Kind)) {
                string action=capability.Action;var button=Button(capability.Label,()=>_action(d.Id,action));
                _actions.Add((button,action));functions.Controls.Add(button);
            }
            functions.Controls.Add(Button("本设备的数据",()=>{using var dialog=new DeviceDataForm(_store,d.Id);if(dialog.ShowDialog(this)==DialogResult.OK){_action(d.Id,"data-changed");Reload();}}));
            functions.SizeChanged+=(_,_)=>{int width=Math.Max(140,(functions.ClientSize.Width-16)/2);foreach(Control b in functions.Controls){b.AutoSize=false;b.Width=width;b.Height=Math.Max(b.MinimumSize.Height,b.GetPreferredSize(Size.Empty).Height);}};
            _detail.Controls.Add(functions);
            AddText("设备管理",9,true);_detail.Controls.Add(management);
            if(_store.Snapshot.LegacyDecisionPending){var legacy=Row();legacy.Controls.Add(Button("导入原小屏配置",()=>_action("","migrate-legacy")));legacy.Controls.Add(Button("暂不使用",()=>_action("","dismiss-legacy")));_detail.Controls.Add(legacy);}
            RefreshStatus();
        }
        _detail.ResumeLayout(true);
    }
    private async Task Change(RegisteredDevice d,bool remove) {
        if(remove&&MessageBox.Show(this,$"移除“{d.Name}”？\n保留配置、配对恢复资料、生日和备份，不清空硬件。","移除设备",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning)!=DialogResult.OK)return;
        Enabled=false;try{await _change(d,remove);Reload();}catch(Exception ex){MessageBox.Show(this,ex.Message,"设备操作未完成");}finally{if(!IsDisposed)Enabled=true;}
    }
    private void Rename(RegisteredDevice d) {
        using var form=new Form{Text="修改设备名称",ClientSize=new(360,125),StartPosition=FormStartPosition.CenterParent,Font=Font,FormBorderStyle=FormBorderStyle.FixedDialog,MinimizeBox=false,MaximizeBox=false};
        var name=new TextBox{Text=d.Name,MaxLength=40,Dock=DockStyle.Top};var save=Button("保存",()=>{try{_store.Update(d with{Name=name.Text.Trim()});form.DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show(form,ex.Message);}},true);
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,Padding=new Padding(16)};name.Width=310;panel.Controls.Add(name);panel.Controls.Add(save);form.Controls.Add(panel);form.AcceptButton=save;
        if(form.ShowDialog(this)==DialogResult.OK){_action(d.Id,"renamed");Reload();}
    }
    private void RefreshStatus() {
        if(Selected is not { } d)return;var v=_view(d);string text=$"{(d.Enabled?v.Status:"已停用")}\n{v.Connection}\n固件：{v.Firmware}";
        if(_state.Text!=text)_state.Text=text;
        foreach(var (button,action) in _actions)button.Enabled=DeviceCapabilities.Allows(d,action)&&(!DeviceCapabilities.RequiresOnline(action)||v.Online);
    }
    private void AddText(string text,float size,bool bold=false)=>_detail.Controls.Add(new Label{Text=text,AutoSize=true,Font=new Font(Font.FontFamily,size,bold?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(0,3,0,6)});
    internal static FlowLayoutPanel Row()=>new(){AutoSize=true,WrapContents=true,Margin=new Padding(0,4,0,8)};
    internal static Button Button(string text,Action click,bool primary=false) {var b=new Button{Text=text,AutoSize=true};SettingsWindow.StyleButton(b,primary);b.Click+=(_,_)=>click();return b;}
    protected override void Dispose(bool disposing){if(disposing){_timer.Stop();_timer.Dispose();_state.Dispose();}base.Dispose(disposing);}
}

internal sealed class DeviceDataForm : Form
{
    internal DeviceDataForm(DeviceRegistryStore store,string id) {
        var d=store.Snapshot.Devices.Single(x=>x.Id==id);Text=$"{d.Name} · 数据选择";Font=new Font("Microsoft YaHei UI",9);ClientSize=new(480,500);MinimumSize=new(420,400);AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new(96,96);StartPosition=FormStartPosition.CenterParent;
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Padding=new Padding(16)};
        root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Percent,45));root.RowStyles.Add(new(SizeType.Percent,55));root.RowStyles.Add(new(SizeType.AutoSize));root.ColumnStyles.Add(new(SizeType.Percent,100));
        root.Controls.Add(new Label{Text="公共账号保持共享，本页只调整此设备的采集需求。",AutoSize=true},0,0);
        var sources=new CheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true};var providers=new CheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true};
        string[] labels=["会话动态","模型额度","天气","股票","音乐","系统状态"];
        for(int i=0;i<DeviceRegistryStore.SourceIds.Length;i++)sources.Items.Add(labels[i],d.Sources.Contains(DeviceRegistryStore.SourceIds[i]));
        foreach(string p in DeviceRegistryStore.ProviderIds)providers.Items.Add(p,d.Providers.Contains(p));
        root.Controls.Add(sources,0,1);root.Controls.Add(providers,0,2);
        root.Controls.Add(DeviceCenterForm.Button("保存",()=>{try{var current=store.Snapshot.Devices.Single(x=>x.Id==id);store.Update(current with {Sources=sources.CheckedIndices.Cast<int>().Select(i=>DeviceRegistryStore.SourceIds[i]).ToArray(),Providers=providers.CheckedItems.Cast<string>().ToArray()});DialogResult=DialogResult.OK;}catch(Exception ex){MessageBox.Show(this,ex.Message);}},true),0,3);Controls.Add(root);
    }
}
