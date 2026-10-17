namespace AIBotBridge;

internal sealed class LegacyDeviceSettingsForm : Form
{
    private readonly ComboBox _mode=new(){DropDownStyle=ComboBoxStyle.DropDownList,Name="esp-connection-mode"};
    internal EspConnectionMode SelectedMode=>(EspConnectionMode)_mode.SelectedIndex;
    internal LegacyDeviceSettingsForm(SerialPublisher? serial=null,EspConnectionMode mode=EspConnectionMode.Auto,Func<DeviceView>? view=null) {
        SuspendLayout();
        var settings=BridgeSettings.Load();Text="ESP8266 · 连接设置";Font=SettingsWindow.BodyFont();AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        ClientSize=new(460,365);MinimumSize=SizeFromClientSize(new(440,360));StartPosition=FormStartPosition.CenterScreen;MaximizeBox=false;MinimizeBox=false;
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(24),ColumnCount=1,RowCount=6};
        root.ColumnStyles.Add(new(SizeType.Percent,100));
        for(int i=0;i<6;i++)root.RowStyles.Add(new(i==4?SizeType.Percent:SizeType.AutoSize,i==4?100:0));
        root.Controls.Add(SettingsWindow.Title("连接设置"),0,0);
        var modeGroup=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=1,Margin=Padding.Empty};modeGroup.ColumnStyles.Add(new(SizeType.Percent,100));
        modeGroup.Controls.Add(new Label{Text="连接方式",AutoSize=true,Margin=new(0,0,0,6)},0,0);
        _mode.Dock=DockStyle.Fill;_mode.Margin=Padding.Empty;
        _mode.Items.AddRange(["自动（默认）","仅 USB","仅 Wi-Fi"]);_mode.SelectedIndex=(int)mode;modeGroup.Controls.Add(_mode,0,1);
        var hint=new Label{AutoSize=true,Dock=DockStyle.Fill,ForeColor=SettingsWindow.Muted,Margin=new(0,8,0,18)};modeGroup.Controls.Add(hint,0,2);
        void Explain()=>hint.Text=SelectedMode switch {
            EspConnectionMode.Usb=>"仅通过 USB 同步，断开后不切换 Wi-Fi。",
            EspConnectionMode.Wifi=>"通过 Wi-Fi 同步，USB 仍可供电和升级。",
            _=>"优先 USB，断开后自动切换 Wi-Fi。"};
        _mode.SelectedIndexChanged+=(_,_)=>Explain();Explain();
        root.Controls.Add(modeGroup,0,1);
        var connection=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=2,Margin=new(0,0,0,18)};
        connection.ColumnStyles.Add(new(SizeType.AutoSize));connection.ColumnStyles.Add(new(SizeType.Percent,100));
        Label Status(string caption,string name,int index) {
            connection.Controls.Add(new Label{Text=caption,AutoSize=true,ForeColor=SettingsWindow.Muted,Margin=new(0,0,20,8)},0,index);
            var value=new Label{Name=name,AutoSize=true,Dock=DockStyle.Fill,Margin=new(0,0,0,8)};connection.Controls.Add(value,1,index);return value;
        }
        var transport=Status("当前通道","esp-current-transport",0);var usb=Status("USB","esp-usb-state",1);var wifi=Status("Wi-Fi","esp-wifi-state",2);
        void RefreshConnection(){var current=view?.Invoke();transport.Text=current?.Transport??(serial?.PortName is null?"无":"USB");usb.Text=current?.Usb??(serial?.PortName is {} port?"已连接 · "+port:"未连接");wifi.Text=current?.Wifi??"未连接";}
        RefreshConnection();
        var timer=new System.Windows.Forms.Timer{Interval=1500};timer.Tick+=(_,_)=>RefreshConnection();timer.Start();Disposed+=(_,_)=>timer.Dispose();
        root.Controls.Add(connection,0,2);
        var saverRow=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=3,Margin=Padding.Empty};
        saverRow.ColumnStyles.Add(new(SizeType.AutoSize));saverRow.ColumnStyles.Add(new(SizeType.Absolute,82));saverRow.ColumnStyles.Add(new(SizeType.Percent,100));
        saverRow.Controls.Add(new Label{Text="屏保等待",AutoSize=true,Anchor=AnchorStyles.Left,Margin=new(0,0,16,0)},0,0);
        var saver=new NumericUpDown{Name="esp-screensaver-minutes",Dock=DockStyle.Fill,Margin=Padding.Empty,Minimum=0,Maximum=1440,Value=int.TryParse(settings.Get("screensaver_timeout_minutes"),out int minutes)?Math.Clamp(minutes,0,1440):0};saverRow.Controls.Add(saver,1,0);
        saverRow.Controls.Add(new Label{Text="分钟，0 为关闭",AutoSize=true,Anchor=AnchorStyles.Left,ForeColor=SettingsWindow.Muted,Margin=new(10,0,0,0)},2,0);root.Controls.Add(saverRow,0,3);
        var row=DeviceCenterForm.Row();row.Dock=DockStyle.Fill;row.Margin=Padding.Empty;row.Padding=new(0,16,0,0);row.FlowDirection=FlowDirection.RightToLeft;row.WrapContents=false;
        var save=DeviceCenterForm.Button("保存",()=> {
            if(!settings.SaveEditable(new Dictionary<string,string>{["screensaver_timeout_minutes"]=((int)saver.Value).ToString()},out string error)){MessageBox.Show(this,error);return;}
            DialogResult=DialogResult.OK;Close();
        },true);var cancel=DeviceCenterForm.Button("取消",Close);cancel.DialogResult=DialogResult.Cancel;cancel.CausesValidation=false;
        row.Controls.Add(cancel);row.Controls.Add(save);CancelButton=cancel;AcceptButton=save;root.Controls.Add(row,0,5);Controls.Add(root);SettingsWindow.FitScreen(this);ResumeLayout(false);
    }
}
