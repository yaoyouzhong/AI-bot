namespace AIBotBridge;

internal sealed class LegacyDeviceSettingsForm : Form
{
    private readonly ComboBox _mode=new(){DropDownStyle=ComboBoxStyle.DropDownList,Name="esp-connection-mode"};
    internal EspConnectionMode SelectedMode=>(EspConnectionMode)_mode.SelectedIndex;
    internal LegacyDeviceSettingsForm(SerialPublisher? serial=null,EspConnectionMode mode=EspConnectionMode.Auto,Func<DeviceView>? view=null) {
        var settings=BridgeSettings.Load();Text="ESP8266 · 连接与屏保";Font=new Font("Microsoft YaHei UI",9F);AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        ClientSize=new(440,350);MinimumSize=new(410,340);StartPosition=FormStartPosition.CenterScreen;
        var panel=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(18)};Controls.Add(panel);SettingsWindow.FitFlow(panel);
        panel.Controls.Add(new Label{Text="连接方式",AutoSize=true});
        _mode.Items.AddRange(["自动（默认）","仅 USB","仅 Wi-Fi"]);_mode.SelectedIndex=(int)mode;panel.Controls.Add(_mode);
        var hint=new Label{AutoSize=true,ForeColor=Color.DimGray,Margin=new Padding(3,6,3,12)};panel.Controls.Add(hint);
        void Explain()=>hint.Text=SelectedMode switch {
            EspConnectionMode.Usb=>"仅使用 USB 数据；连接中断时不切换 Wi-Fi。",
            EspConnectionMode.Wifi=>"数据仅走 Wi-Fi；USB 可用于供电、配置和升级。",
            _=>"优先 USB，USB 数据中断后自动转 Wi-Fi。"};
        _mode.SelectedIndexChanged+=(_,_)=>Explain();Explain();
        var connection=new Label{AutoSize=true};panel.Controls.Add(connection);
        void RefreshConnection(){var current=view?.Invoke();connection.Text=current is null?"USB："+(serial?.PortName is {} port?"已连接 · "+port:"未连接"):$"当前通道：{current.Transport??"无"}\nUSB：{current.Usb??"未连接"}    Wi-Fi：{current.Wifi??"未连接"}";}
        RefreshConnection();
        var timer=new System.Windows.Forms.Timer{Interval=1500};timer.Tick+=(_,_)=>RefreshConnection();timer.Start();Disposed+=(_,_)=>timer.Dispose();
        panel.Controls.Add(new Label{Text="屏保等待（分钟，0 为关闭）",AutoSize=true,Margin=new Padding(3,12,3,3)});
        var saver=new NumericUpDown{Minimum=0,Maximum=1440,Value=int.TryParse(settings.Get("screensaver_timeout_minutes"),out int minutes)?Math.Clamp(minutes,0,1440):0};panel.Controls.Add(saver);
        var row=DeviceCenterForm.Row();row.Controls.Add(DeviceCenterForm.Button("保存",()=> {
            if(!settings.SaveEditable(new Dictionary<string,string>{["screensaver_timeout_minutes"]=((int)saver.Value).ToString()},out string error)){MessageBox.Show(this,error);return;}
            DialogResult=DialogResult.OK;
        },true));var cancel=DeviceCenterForm.Button("取消",Close);row.Controls.Add(cancel);CancelButton=cancel;panel.Controls.Add(row);SettingsWindow.FitScreen(this);
    }
}
