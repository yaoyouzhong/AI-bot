namespace AIBotBridge;
internal sealed record NotificationOptions(bool Completion=true,bool Attention=true,bool Quota=true,bool Sound=true,bool Quiet=false,int QuietStart=1320,int QuietEnd=480,bool Health=true)
{
    internal void Validate(){if(QuietStart is <0 or >=1440||QuietEnd is <0 or >=1440||Quiet&&QuietStart==QuietEnd)throw new InvalidOperationException("勿扰开始和结束时间须不同。");}
    internal bool IsQuiet(DateTime local){int minute=local.Hour*60+local.Minute;return Quiet&&(QuietStart<QuietEnd?minute>=QuietStart&&minute<QuietEnd:minute>=QuietStart||minute<QuietEnd);}
}
internal sealed record NotificationEntry(DateTimeOffset At,string Kind,string Title,string Outcome);
internal sealed record NotificationDecision(bool Show,bool Sound);
internal sealed partial class Tab5Service
{
    // Delivery follows fresh authenticated traffic, not a configured device entry.
    // Device mute remains an intentional silence, rather than waking PC audio.
    internal bool CompletionNoticeAvailable {
        get {long now=Environment.TickCount64;bool Fresh(long at)=>at>0&&now-at<8000;
            return Fresh(Interlocked.Read(ref _usbAckAt))||Fresh(Interlocked.Read(ref _wifiRequestAt))||Fresh(Interlocked.Read(ref _bleAckAt));}
    }
}
internal sealed class BridgeNotifications
{
    private NotificationOptions? _options;
    private readonly List<NotificationEntry> _history=[];
    private readonly Dictionary<string,DateTimeOffset> _seen=[];
    private readonly Dictionary<string,DateTimeOffset> _lastShown=[];
    private NotificationEntry? _pendingBalloon;
    internal string? Error {get;private set;}
    internal NotificationOptions Options {
        get{if(_options is not null)return _options;try{var value=UserPreferenceFile.Read<NotificationOptions>("notifications.json")??new();value.Validate();_options=value;}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException){Error="提醒设置未能读取，暂用默认设置："+ex.Message;_options=new();}return _options;}
    }
    internal void Save(NotificationOptions value){value.Validate();UserPreferenceFile.Write("notifications.json",value);_options=value;Error=null;}
    internal NotificationEntry[] History=>_history.AsEnumerable().Reverse().ToArray();
    internal void BalloonShown() {
        if(_pendingBalloon is {} pending&&DateTimeOffset.Now-pending.At<TimeSpan.FromSeconds(30)) {
            int index=_history.IndexOf(pending);if(index>=0)_history[index]=pending with {Outcome="系统已显示"};
        }
        _pendingBalloon=null;
    }
    internal void IgnoreUntrackedBalloon()=>_pendingBalloon=null;
    internal NotificationDecision Evaluate(string kind,string key,string title,DateTimeOffset at,bool completionOnDevice=false) {
        if(kind is not ("completion" or "attention" or "quota" or "health"))throw new ArgumentException("Unknown notification kind");
        var options=Options;
        foreach(var old in _seen.Where(p=>at-p.Value>TimeSpan.FromHours(24)).Select(p=>p.Key).ToArray())_seen.Remove(old);
        string identity=kind+":"+key;if(_seen.ContainsKey(identity))return new(false,false);
        if(_seen.Count>=512)_seen.Remove(_seen.MinBy(p=>p.Value).Key);_seen[identity]=at;
        bool enabled=kind switch{"completion"=>options.Completion,"attention"=>options.Attention,"health"=>options.Health,_=>options.Quota};
        string outcome=kind=="completion"&&completionOnDevice?"由 TAB5 提醒":!enabled?"已关闭":options.IsQuiet(at.LocalDateTime)?"勿扰时段":
            _lastShown.TryGetValue(kind,out var previous)&&at-previous<TimeSpan.FromSeconds(kind=="quota"?300:10)?"合并频繁提醒":"已请求通知";
        bool deliver=outcome=="已请求通知",show=deliver&&kind!="completion";if(deliver)_lastShown[kind]=at;
        if(deliver&&kind=="completion")outcome=options.Sound?"已请求备用音":"提醒音已关闭";
        var entry=new NotificationEntry(at,kind,title,outcome);_history.Add(entry);if(show)_pendingBalloon=entry;
        if(_history.Count>100)_history.RemoveAt(0);
        return new(show,deliver&&options.Sound&&kind is "completion" or "health");
    }
}
internal sealed class NotificationSettingsForm : Form
{
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 2000 };
    internal NotificationSettingsForm(BridgeNotifications notifications)
    {
        Text = "AI-bot · 电脑提醒";
        Font = SettingsWindow.BodyFont();
        AutoScaleDimensions = new(96, 96);
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new(580, 400);
        MinimumSize = SizeFromClientSize(new(465, 320));
        var tabs = new TabControl { Dock = DockStyle.Fill, Padding = new(14, 6) };
        var settings = new TabPage("提醒设置") { BackColor = Color.White, Padding = new(16) };
        var history = new TabPage("提醒记录") { BackColor = Color.White, Padding = new(8) };
        tabs.TabPages.AddRange([settings, history]);
        Padding = new(12);
        Controls.Add(tabs);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        layout.ColumnStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.Percent, 100));
        layout.RowStyles.Add(new(SizeType.AutoSize));
        settings.Controls.Add(layout);
        var viewport = new Panel { Dock = DockStyle.Fill };
        var options = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2 };
        options.ColumnStyles.Add(new(SizeType.Percent, 50));
        options.ColumnStyles.Add(new(SizeType.Percent, 50));
        viewport.Controls.Add(options);
        layout.Controls.Add(viewport, 0, 0);

        var value = notifications.Options;
        Label Section(string title) => new() { Text = title, Font = SettingsWindow.SectionFont(), AutoSize = true, Margin = new(3, 4, 3, 6) };
        CheckBox Toggle(string title, bool enabled) => new() { Text = title, Checked = enabled, AutoSize = true, Margin = new(3, 4, 3, 4) };
        var types = Section("提醒内容");
        options.Controls.Add(types, 0, 0); options.SetColumnSpan(types, 2);
        var attention = Toggle("等待操作", value.Attention);
        var quota = Toggle("额度异常", value.Quota);
        var health = Toggle("运行异常", value.Health);
        var completion = Toggle("完成备用音", value.Completion);
        options.Controls.Add(attention, 0, 1); options.Controls.Add(quota, 1, 1);
        options.Controls.Add(health, 0, 2); options.Controls.Add(completion, 1, 2);
        var delivery = Section("声音与勿扰");
        delivery.Margin = new(3, 14, 3, 6);
        options.Controls.Add(delivery, 0, 3); options.SetColumnSpan(delivery, 2);
        var sound = Toggle("提醒音", value.Sound);
        var quiet = Toggle("勿扰时段", value.Quiet);
        options.Controls.Add(sound, 0, 4);
        DateTimePicker Time(int minute) => new() { Format = DateTimePickerFormat.Custom, CustomFormat = "HH:mm",
            ShowUpDown = true, Value = DateTime.Today.AddMinutes(minute), Width = 92 };
        var start = Time(value.QuietStart); var end = Time(value.QuietEnd);
        var hours = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Dock = DockStyle.Fill };
        hours.Controls.AddRange([quiet, start, new Label { Text = "至", AutoSize = true, Anchor = AnchorStyles.Left }, end]);
        options.Controls.Add(hours, 0, 5); options.SetColumnSpan(hours, 2);
        start.Enabled = end.Enabled = quiet.Checked;
        quiet.CheckedChanged += (_, _) => start.Enabled = end.Enabled = quiet.Checked;

        var footer = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2, Padding = new(0, 8, 0, 0) };
        footer.ColumnStyles.Add(new(SizeType.Percent, 100)); footer.ColumnStyles.Add(new(SizeType.AutoSize));
        var status = new Label { Text = notifications.Error, AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = SettingsWindow.Muted };
        var save = new Button { Text = "保存设置" };
        SettingsWindow.StyleButton(save, true);
        footer.Controls.Add(status, 0, 0); footer.Controls.Add(save, 1, 0); layout.Controls.Add(footer, 0, 1);
        save.Click += (_, _) => {
            try {
                notifications.Save(new(completion.Checked, attention.Checked, quota.Checked, sound.Checked, quiet.Checked,
                    start.Value.Hour*60+start.Value.Minute, end.Value.Hour*60+end.Value.Minute, health.Checked));
                status.Text = "已保存";
            } catch (Exception ex) { status.Text = ex.Message; }
        };

        var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells, BackgroundColor = Color.White };
        foreach (string title in new[] { "时间", "提醒", "处理" })
            grid.Columns[grid.Columns.Add(title,title)].SortMode = DataGridViewColumnSortMode.NotSortable;
        grid.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        history.Controls.Add(grid);
        var empty = new Label { Text = "暂无提醒记录", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, ForeColor = SettingsWindow.Muted };
        history.Controls.Add(empty);
        NotificationEntry[] displayed = [];
        void RefreshHistory()
        {
            var entries = notifications.History;
            empty.Visible = entries.Length == 0;
            grid.Visible = entries.Length > 0;
            if (displayed.SequenceEqual(entries)) return;
            displayed = entries;
            grid.Rows.Clear();
            foreach (var entry in entries) grid.Rows.Add(entry.At.ToLocalTime().ToString("MM-dd HH:mm:ss"), entry.Title, entry.Outcome);
        }
        tabs.SelectedIndexChanged += (_, _) => {
            _timer.Enabled = tabs.SelectedTab == history;
            if (_timer.Enabled) RefreshHistory();
        };
        _timer.Tick += (_, _) => RefreshHistory();
        Shown += (_, _) => {
            // Size from the rendered font, tab header, footer and DPI-scaled controls.
            // Toggling quiet hours only changes availability, so the layout stays still.
            int chromeWidth = Width - viewport.ClientSize.Width;
            int chromeHeight = Height - viewport.ClientSize.Height;
            var hoursSize = hours.GetPreferredSize(Size.Empty);
            int minWidth = Math.Max(MinimumSize.Width, chromeWidth + hoursSize.Width + hours.Margin.Horizontal);
            int minHeight = chromeHeight + options.GetPreferredSize(new(viewport.ClientSize.Width, 0)).Height;
            MinimumSize = new(minWidth, minHeight);
        };
        SettingsWindow.FitScreen(this);
    }
    protected override void Dispose(bool disposing)
    {
        if (disposing) _timer.Dispose();
        base.Dispose(disposing);
    }
}
