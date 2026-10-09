#nullable disable
using System.Drawing;

namespace AIBotBridge.MigratedWeather;

sealed class WeatherSettingsForm : Form
{
    readonly WeatherMonitor _weather;
    readonly TextBox _host = new() { Name="weather-host", Dock = DockStyle.Fill, PlaceholderText="abc.qweatherapi.com" };
    readonly TextBox _city = new() { Name="weather-city", Dock = DockStyle.Fill, PlaceholderText="例如：南京市雨花台区" };
    readonly TextBox _apiKey = new() { Name="weather-api-key", Dock = DockStyle.Fill, UseSystemPasswordChar = true };
    readonly RadioButton _autoLocation = new() { Name="weather-auto-location", Text = "使用电脑当前位置", AutoSize = true };
    readonly RadioButton _manualLocation = new() { Name="weather-manual-location", Text = "指定城市或区县", AutoSize = true };
    readonly TableLayoutPanel _cityGroup;
    readonly TableLayoutPanel _locationRow;
    readonly RadioButton _free = new() { Name="weather-source-free",Text="免费天气 · Open-Meteo",AutoSize=true };
    readonly RadioButton _qweather = new() { Name="weather-source-qweather",Text="和风天气",AutoSize=true };
    readonly TableLayoutPanel _credentials;
    readonly Label _location = new() { AutoSize = true, ForeColor = Color.FromArgb(90, 106, 123) };
    readonly Label _status = new() { Name="weather-result",AutoSize = true, Padding = new Padding(10,6,10,6), BackColor = Color.FromArgb(232, 240, 249),Visible=false };
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
        Text = "AI-bot · 天气与定位";
        ClientSize = new Size(620, 570);
        MinimumSize = new Size(560, 480);
        StartPosition = FormStartPosition.CenterScreen;
        MaximizeBox = false;
        BackColor = Color.FromArgb(247, 249, 252);
        ForeColor = Color.FromArgb(29, 44, 63);

        var root = new TableLayoutPanel { Dock=DockStyle.Fill, ColumnCount=1, RowCount=4, Margin=Padding.Empty };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        var header = new TableLayoutPanel { Dock=DockStyle.Fill, AutoSize=true, ColumnCount=1, Padding=new Padding(24,18,24,14), Margin=Padding.Empty, BackColor=Color.White };
        header.Controls.Add(new Label { Text="天气与定位", AutoSize=true, Font=new Font(Font.FontFamily,16F,FontStyle.Bold), Margin=new Padding(0,0,0,6) });
        header.Controls.Add(new Label { Text="设置天气位置与数据源，TAB5 和小屏共用。", AutoSize=true, ForeColor=Color.FromArgb(90,106,123), Margin=Padding.Empty });
        root.Controls.Add(header,0,0);
        var body = new FlowLayoutPanel { Name="weather-body", Dock=DockStyle.Fill, FlowDirection=FlowDirection.TopDown, WrapContents=false, AutoScroll=true, Padding=new Padding(24,18,20,14), Margin=Padding.Empty };
        body.Controls.Add(new Label{Text="天气位置",AutoSize=true,Font=new Font(Font,FontStyle.Bold),Margin=new Padding(0,0,0,10)});
        var modes=new FlowLayoutPanel{AutoSize=true,Margin=new Padding(0,0,0,12)};
        _autoLocation.Margin=new Padding(0,0,20,0);_manualLocation.Margin=Padding.Empty;modes.Controls.AddRange([_autoLocation,_manualLocation]);body.Controls.Add(modes);
        var locationDetails=new Panel{Name="weather-location-details",Margin=new Padding(0,0,0,14)};
        var locationRow = _locationRow = new TableLayoutPanel { AutoSize=true, Dock=DockStyle.Top, ColumnCount=2, Margin=Padding.Empty };
        locationRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        locationRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        locationRow.Controls.Add(_location,0,0);
        locationRow.Controls.Add(_locate,1,0);
        locationDetails.Controls.Add(locationRow);body.Controls.Add(locationDetails);
        _location.Margin=new Padding(0,5,10,0);_location.Dock=DockStyle.Fill;
        locationRow.SizeChanged+=(_,_)=>_location.MaximumSize=new Size(Math.Max(100,locationRow.ClientSize.Width-_locate.Width-20),0);
        _cityGroup=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Top,ColumnCount=1,Margin=Padding.Empty};_cityGroup.ColumnStyles.Add(new(SizeType.Percent,100));
        _city.Margin=new(0,0,0,4);_cityGroup.Controls.Add(_city,0,0);
        var cityNote=SourceNote("固定地区，不随电脑移动。",Padding.Empty);_cityGroup.Controls.Add(cityNote,0,1);locationDetails.Controls.Add(_cityGroup);
        _cityGroup.SizeChanged+=(_,_)=>cityNote.MaximumSize=new(Math.Max(100,_cityGroup.Width),0);
        bool fittingLocation=false;
        void FitLocation(){
            if(fittingLocation)return;fittingLocation=true;
            try {
                locationRow.Width=_cityGroup.Width=locationDetails.ClientSize.Width;
                // Hidden TableLayoutPanel children are excluded from preferred size. Measure
                // both alternatives explicitly to keep the source anchored on every switch.
                int manualHeight=_city.PreferredHeight+_city.Margin.Vertical+cityNote.GetPreferredSize(new(Math.Max(100,locationDetails.Width),0)).Height;
                int autoHeight=Math.Max(_locate.GetPreferredSize(Size.Empty).Height+_locate.Margin.Vertical,
                    _location.GetPreferredSize(new(Math.Max(100,locationDetails.Width-_locate.Width-20),0)).Height+_location.Margin.Vertical);
                int height=Math.Max(manualHeight,autoHeight);
                if(locationDetails.Height!=height)locationDetails.Height=height;
            }finally{fittingLocation=false;}
        }
        locationDetails.Layout+=(_,_)=>FitLocation();
        body.Controls.Add(new Label{Name="weather-source-heading",Text="天气数据源",AutoSize=true,Font=new Font(Font,FontStyle.Bold),Margin=new Padding(0,4,0,10)});
        var sources=new TableLayoutPanel{AutoSize=true,ColumnCount=1,Margin=new(0,0,0,8)};
        sources.ColumnStyles.Add(new(SizeType.Percent,100));
        // Both radios share a parent: changing the source is exclusive and independent of location.
        sources.Controls.Add(_free,0,0);_free.Margin=new(0,0,0,4);
        var freeNote=SourceNote("免密钥，个人非商业免费；数值预报，美标 AQI。",new(22,0,0,12));sources.Controls.Add(freeNote,0,1);
        sources.Controls.Add(_qweather,0,2);_qweather.Margin=new(0,0,0,4);
        var paidNote=SourceNote("需 Host / Key；中文区县、当地 AQI，按账户额度计费。",new(22,0,0,8));sources.Controls.Add(paidNote,0,3);
        var commonNote=SourceNote("均支持逐小时和 7 天预报；天气数值与 AQI 口径可能不同。",Padding.Empty);sources.Controls.Add(commonNote,0,4);
        sources.SizeChanged+=(_,_)=>{foreach(var note in new[]{freeNote,paidNote,commonNote})note.MaximumSize=new(Math.Max(100,sources.ClientSize.Width-note.Margin.Horizontal),0);};body.Controls.Add(sources);
        _credentials=new TableLayoutPanel{Name="weather-credentials",AutoSize=true,ColumnCount=2,Margin=Padding.Empty,Padding=new(10),BackColor=Color.FromArgb(235,240,247)};
        _credentials.ColumnStyles.Add(new(SizeType.AutoSize));_credentials.ColumnStyles.Add(new(SizeType.Percent,100));
        _credentials.Controls.Add(new Label{Text="API Host",AutoSize=true,Margin=new(0,5,12,10)},0,0);_host.Margin=new(0,0,0,10);_credentials.Controls.Add(_host,1,0);
        _credentials.Controls.Add(new Label{Text="API Key",AutoSize=true,Margin=new(0,5,12,8)},0,1);_apiKey.Margin=new(0,0,0,8);_credentials.Controls.Add(_apiKey,1,1);
        var keyNote=SourceNote("Host：和风控制台 → 设置；密钥留空保留，存于 Windows 凭据管理器。",Padding.Empty);_credentials.Controls.Add(keyNote,0,2);_credentials.SetColumnSpan(keyNote,2);
        _credentials.SizeChanged+=(_,_)=>keyNote.MaximumSize=new(Math.Max(100,_credentials.ClientSize.Width-_credentials.Padding.Horizontal),0);body.Controls.Add(_credentials);
        var fallback=SourceNote("和风请求失败时暂用免费天气；两者均失败则保留上次数据。",new(0,8,0,0));body.Controls.Add(fallback);
        SettingsWindow.FitFlow(body);
        root.Controls.Add(body,0,1);
        _status.Margin=new Padding(24,10,24,0);_status.Dock=DockStyle.Fill;root.Controls.Add(_status,0,2);
        root.SizeChanged+=(_,_)=>_status.MaximumSize=new Size(Math.Max(100,root.ClientSize.Width-_status.Margin.Horizontal),0);

        var footer = new TableLayoutPanel { Name="weather-actions", Dock=DockStyle.Fill, AutoSize=true, ColumnCount=3, Padding=new Padding(24,14,24,14), Margin=Padding.Empty, BackColor=Color.White };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var cancel = new Button { Text="取消", DialogResult=DialogResult.Cancel };
        foreach(var button in new[]{_locate,_test,_save,cancel})SettingsWindow.StyleButton(button,button==_save);
        _test.Margin=Padding.Empty; cancel.Margin=new Padding(0,0,10,0); _save.Margin=Padding.Empty;
        footer.Controls.Add(_test,0,0); footer.Controls.Add(cancel,1,0); footer.Controls.Add(_save,2,0);
        root.Controls.Add(footer,0,3);
        Controls.Add(root);

        _host.Text=WeatherMonitor.QWeatherApiHost;
        _city.Text=WeatherMonitor.City;
        _autoLocation.Checked=WeatherMonitor.AutoLocation;
        _manualLocation.Checked=!_autoLocation.Checked;
        _latitude=WeatherMonitor.Latitude; _longitude=WeatherMonitor.Longitude;
        _apiKey.PlaceholderText=WeatherMonitor.HasQWeatherApiKey ? "已保存密钥" : "填写和风 API Key";
        _qweather.Checked=WeatherMonitor.Provider=="qweather";_free.Checked=!_qweather.Checked;
        UpdateSourceState();
        UpdateLocationState();
        _autoLocation.CheckedChanged+=(_,_)=>{UpdateLocationState();_status.Visible=false;};
        _qweather.CheckedChanged+=(_,_)=>{UpdateSourceState();_status.Visible=false;};
        _locate.Click+=async(_,_)=>await Locate();
        _test.Click+=async(_,_)=>await TestConnection();
        _save.Click+=(_,_)=>SaveSettings();
        AcceptButton=_save; CancelButton=cancel;
        cancel.CausesValidation=false;
        cancel.Click+=(_,_)=>Close();
        SettingsWindow.FitScreen(this);
        ResumeLayout(true);
    }

    static Label SourceNote(string text,Padding margin)=>new(){Text=text,AutoSize=true,ForeColor=Color.FromArgb(90,106,123),Margin=margin};
    string SelectedProvider=>_qweather.Checked?"qweather":"open-meteo";
    void UpdateSourceState()=>_credentials.Visible=_qweather.Checked;

    void UpdateLocationState()
    {
        _city.Enabled=!_busy&&!_autoLocation.Checked;
        _cityGroup.Visible=!_autoLocation.Checked;
        _locationRow.Visible=_autoLocation.Checked;
        _locate.Enabled=!_busy&&_autoLocation.Checked;
        _locate.Visible=_autoLocation.Checked;
        _location.Text=_latitude!=0||_longitude!=0 ? _weather.LocationStatus.Contains("不可用")?"定位不可用，暂用上次位置。可重新获取。":"已有定位，随电脑移动更新。"
            : "未定位，请获取位置并允许 Windows 定位。";
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
        SetBusy(true,"正在测试天气连接…");
        try {
            var snapshot=await _weather.TestSettings(_host.Text,_apiKey.Text,_city.Text,_autoLocation.Checked,_latitude,_longitude,SelectedProvider);
            if(!IsDisposed)_status.Text=$"连接成功（{(snapshot.Source=="open-meteo"?"Open-Meteo":"和风天气")}）：{snapshot.City} · {snapshot.Condition} · {snapshot.Temperature:F0}℃ · 空气{snapshot.AirQuality}";
        }
        catch(Exception ex) { if(!IsDisposed)_status.Text=ex.Message.Trim(); }
        finally { if(!IsDisposed)SetBusy(false); }
    }

    void SaveSettings()
    {
        if(_busy)return;
        _status.Visible=true;
        if(!_autoLocation.Checked&&string.IsNullOrWhiteSpace(_city.Text)) {_status.Text="请输入地区。";return;}
        if(_autoLocation.Checked&&_latitude==0&&_longitude==0) {_status.Text="请先获取当前位置。";return;}
        try {
            _weather.ApplySettings(_host.Text,_city.Text,_autoLocation.Checked,_latitude,_longitude,_apiKey.Text,SelectedProvider);
            DialogResult=DialogResult.OK; Close();
        }
        catch(Exception ex) {_status.Text=ex.Message.Trim();}
    }

    void SetBusy(bool busy,string message=null)
    {
        _busy=busy;
        _autoLocation.Enabled=_manualLocation.Enabled=_free.Enabled=_qweather.Enabled=_host.Enabled=_apiKey.Enabled=_test.Enabled=_save.Enabled=!busy;
        UpdateLocationState();
        if(message!=null){_status.Text=message;_status.Visible=true;}
        UseWaitCursor=busy;
    }

    internal void UsePreviewData()
    {
        _host.Text="example.qweatherapi.com"; _city.Text="南京市秦淮区"; _apiKey.Clear();
        _apiKey.PlaceholderText="已保存密钥"; _autoLocation.Checked=true;_qweather.Checked=true;
        _latitude=32; _longitude=119; UpdateLocationState();
        _location.Text="定位不可用，暂用上次位置。可重新获取。";
    }
}
