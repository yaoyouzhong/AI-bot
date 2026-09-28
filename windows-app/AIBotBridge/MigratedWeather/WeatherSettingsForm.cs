#nullable disable
using System.Drawing;

namespace AIBotBridge.MigratedWeather;

sealed class WeatherSettingsForm : Form
{
    readonly WeatherMonitor _weather;
    readonly TextBox _host = new() { Dock = DockStyle.Fill };
    readonly TextBox _city = new() { Dock = DockStyle.Fill };
    readonly TextBox _apiKey = new() { Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    readonly CheckBox _autoLocation = new() { Text = "使用 Windows 自动定位", AutoSize = true, Anchor = AnchorStyles.Left };
    readonly Label _location = new() { AutoSize = true, ForeColor = Color.FromArgb(90, 106, 123) };
    readonly Label _status = new() { AutoSize = true, Padding = new Padding(12), BackColor = Color.FromArgb(232, 240, 249) };
    readonly Button _locate = new() { Text = "获取当前位置" };
    readonly Button _test = new() { Text = "测试连接" };
    readonly Button _save = new() { Text = "保存并刷新" };
    double _latitude;
    double _longitude;
    bool _busy;

    public WeatherSettingsForm(WeatherMonitor weather)
    {
        _weather = weather;
        SuspendLayout();
        AutoScaleDimensions = new SizeF(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Microsoft YaHei UI", 9F);
        Text = "天气设置";
        ClientSize = new Size(600, 540);
        MinimumSize = new Size(560, 480);
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        BackColor = Color.FromArgb(247, 249, 252);
        ForeColor = Color.FromArgb(29, 44, 63);

        var root = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=3, Margin=Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var header = new TableLayoutPanel { Dock=DockStyle.Fill, AutoSize=true, ColumnCount=1, Padding=new Padding(24,18,24,14), Margin=Padding.Empty, BackColor=Color.White };
        header.Controls.Add(new Label { Text="天气与定位", AutoSize=true, Font=new Font(Font.FontFamily,16F,FontStyle.Bold), Margin=new Padding(0,0,0,6) });
        header.Controls.Add(new Label { Text="和风天气优先 · Open-Meteo 自动回退", AutoSize=true, ForeColor=Color.FromArgb(90,106,123), Margin=Padding.Empty });
        root.Controls.Add(header,0,0);
        var body = new FlowLayoutPanel { Name="weather-body", Dock=DockStyle.Fill, FlowDirection=FlowDirection.TopDown, WrapContents=false, AutoScroll=true, Padding=new Padding(24,18,20,14), Margin=Padding.Empty };
        AddField(body,"API Host",_host,"和风控制台 → 设置，例如 abc.qweatherapi.com");
        AddField(body,"API Key",_apiKey,"保存在 Windows 凭据管理器；留空保留已有密钥。");
        var locationRow = new TableLayoutPanel { AutoSize=true, ColumnCount=2, Margin=new Padding(0,4,0,8) };
        locationRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        locationRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        locationRow.Controls.Add(_autoLocation,0,0);
        locationRow.Controls.Add(_locate,1,0);
        body.Controls.Add(locationRow);
        _location.Margin=new Padding(0,0,0,14);
        body.Controls.Add(_location);
        AddField(body,"手动地区",_city,"关闭自动定位后可填写城市或区县，例如 南京市秦淮区。");
        _status.Margin=new Padding(0,0,0,0);
        body.Controls.Add(_status);
        SettingsWindow.FitFlow(body);
        root.Controls.Add(body,0,1);

        var footer = new TableLayoutPanel { Name="weather-actions", Dock=DockStyle.Fill, AutoSize=true, ColumnCount=3, Padding=new Padding(24,14,24,14), Margin=Padding.Empty, BackColor=Color.White };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var cancel = new Button { Text="取消", DialogResult=DialogResult.Cancel };
        foreach(var button in new[]{_locate,_test,_save,cancel})SettingsWindow.StyleButton(button,button==_save);
        _test.Margin=Padding.Empty; cancel.Margin=new Padding(0,0,10,0); _save.Margin=Padding.Empty;
        footer.Controls.Add(_test,0,0); footer.Controls.Add(cancel,1,0); footer.Controls.Add(_save,2,0);
        root.Controls.Add(footer,0,2);
        Controls.Add(root);

        _host.Text=WeatherMonitor.QWeatherApiHost;
        _city.Text=WeatherMonitor.City;
        _autoLocation.Checked=WeatherMonitor.AutoLocation;
        _latitude=WeatherMonitor.Latitude; _longitude=WeatherMonitor.Longitude;
        _apiKey.PlaceholderText=WeatherMonitor.HasQWeatherApiKey ? "已保存密钥" : "输入 API Key";
        _status.Text=_autoLocation.Checked && _weather.LocationStatus.Length>0 ? _weather.LocationStatus
            : WeatherMonitor.HasQWeatherApiKey ? "凭据已保存。可测试连接，确认天气地区。" : "填写数据源后测试连接。";
        UpdateLocationState();
        _autoLocation.CheckedChanged+=(_,_)=>UpdateLocationState();
        _locate.Click+=async(_,_)=>await Locate();
        _test.Click+=async(_,_)=>await TestConnection();
        _save.Click+=(_,_)=>SaveSettings();
        AcceptButton=_save; CancelButton=cancel;
        cancel.CausesValidation=false;
        cancel.Click+=(_,_)=>Close();
        SettingsWindow.FitScreen(this);
        ResumeLayout(true);
    }

    static void AddField(FlowLayoutPanel body,string title,Control input,string hint)
    {
        var group=new TableLayoutPanel { AutoSize=true, ColumnCount=1, Margin=new Padding(0,0,0,14) };
        group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        group.Controls.Add(new Label { Text=title, AutoSize=true, Margin=new Padding(0,0,0,5) },0,0);
        input.Margin=new Padding(0,0,0,5); group.Controls.Add(input,0,1);
        var note=new Label { Text=hint, AutoSize=true, ForeColor=Color.FromArgb(90,106,123), Margin=Padding.Empty };
        group.Controls.Add(note,0,2);
        group.SizeChanged+=(_,_)=>note.MaximumSize=new Size(Math.Max(100,group.ClientSize.Width),0);
        body.Controls.Add(group);
    }

    void UpdateLocationState()
    {
        _city.Enabled=!_busy&&!_autoLocation.Checked;
        _locate.Enabled=!_busy&&_autoLocation.Checked;
        _location.Text=!_autoLocation.Checked ? "天气将使用下方填写的地区。"
            : _latitude!=0||_longitude!=0 ? "已有定位。电脑移动后会自动更新；可重新获取以授权或校准。"
            : "尚未获取定位。首次使用时，请允许 Windows 访问位置。";
    }

    async Task Locate()
    {
        if(_busy)return;
        SetBusy(true,"正在获取 Windows 位置，请留意系统授权提示…");
        try {
            var result=await WindowsLocation.Locate();
            if(IsDisposed)return;
            _latitude=result.Latitude; _longitude=result.Longitude;
            UpdateLocationState();
            _status.Text="定位成功。点击“测试连接”核对区县，再保存。";
        }
        catch(Exception ex) { if(!IsDisposed)_status.Text=ex.Message.Trim(); }
        finally { if(!IsDisposed)SetBusy(false); }
    }

    async Task TestConnection()
    {
        if(_busy)return;
        SetBusy(true,"正在连接和风天气…");
        try {
            var snapshot=await _weather.TestQWeather(_host.Text,_apiKey.Text,_city.Text,_autoLocation.Checked,_latitude,_longitude);
            if(!IsDisposed)_status.Text=$"连接成功：{snapshot.City} · {snapshot.Condition} · {snapshot.Temperature:F0}℃ · 空气{snapshot.AirQuality}";
        }
        catch(Exception ex) { if(!IsDisposed)_status.Text=ex.Message.Trim(); }
        finally { if(!IsDisposed)SetBusy(false); }
    }

    void SaveSettings()
    {
        if(_busy)return;
        if(string.IsNullOrWhiteSpace(_host.Text)) {_status.Text="请输入 API Host。";return;}
        if(!_autoLocation.Checked&&string.IsNullOrWhiteSpace(_city.Text)) {_status.Text="请输入地区。";return;}
        if(_autoLocation.Checked&&_latitude==0&&_longitude==0) {_status.Text="请先获取当前位置。";return;}
        if(string.IsNullOrWhiteSpace(_apiKey.Text)&&!WeatherMonitor.HasQWeatherApiKey) {_status.Text="请输入 API Key。";return;}
        try {
            _weather.ApplySettings(_host.Text,_city.Text,_autoLocation.Checked,_latitude,_longitude,_apiKey.Text);
            DialogResult=DialogResult.OK; Close();
        }
        catch(Exception ex) {_status.Text=ex.Message.Trim();}
    }

    void SetBusy(bool busy,string message=null)
    {
        _busy=busy;
        _autoLocation.Enabled=_host.Enabled=_apiKey.Enabled=_test.Enabled=_save.Enabled=!busy;
        UpdateLocationState();
        if(message!=null)_status.Text=message;
        UseWaitCursor=busy;
    }

    internal void UsePreviewData()
    {
        _host.Text="example.qweatherapi.com"; _city.Text="南京市秦淮区"; _apiKey.Clear();
        _apiKey.PlaceholderText="已保存密钥"; _autoLocation.Checked=true;
        _latitude=32; _longitude=119; UpdateLocationState();
        _status.Text="定位权限不可用，暂用上次位置。请点击“获取当前位置”，并在 Windows 提示中允许定位。";
    }
}
