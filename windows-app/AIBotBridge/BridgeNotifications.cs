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
    private readonly System.Windows.Forms.Timer _timer=new(){Interval=2000};
    private bool _disposed;
    internal NotificationSettingsForm(BridgeNotifications notifications) {
        Text="AI-bot · 电脑提醒";Font=SettingsWindow.BodyFont();AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new(96,96);ClientSize=new(600,390);MinimumSize=new(540,330);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(24),ColumnCount=1,RowCount=5};for(int i=0;i<5;i++)root.RowStyles.Add(new(i==3?SizeType.Percent:SizeType.AutoSize,i==3?100:0));root.ColumnStyles.Add(new(SizeType.Percent,100));Controls.Add(root);
        var value=notifications.Options;var checks=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
        CheckBox Box(string text,bool selected){var box=new CheckBox{Text=text,Checked=selected,AutoSize=true};checks.Controls.Add(box);return box;}
        var completion=Box("完成备用音",value.Completion);var attention=Box("等待操作",value.Attention);var quota=Box("额度异常",value.Quota);var health=Box("运行异常",value.Health);var sound=Box("提醒音",value.Sound);root.Controls.Add(checks,0,0);
        var quiet=new CheckBox{Text="勿扰时段",Checked=value.Quiet,AutoSize=true};
        DateTimePicker Time(int minute)=>new(){Format=DateTimePickerFormat.Custom,CustomFormat="HH:mm",ShowUpDown=true,Value=DateTime.Today.AddMinutes(minute),Width=90};
        var start=Time(value.QuietStart);var end=Time(value.QuietEnd);var separator=new Label{Text="—",AutoSize=true};var hours=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};hours.Controls.AddRange([quiet,start,separator,end]);root.Controls.Add(hours,0,1);
        void QuietVisibility(){start.Visible=end.Visible=separator.Visible=quiet.Checked;}quiet.CheckedChanged+=(_,_)=>QuietVisibility();QuietVisibility();
        var info=new Label{Text=notifications.Error??"提醒记录",AutoSize=true,Dock=DockStyle.Fill,ForeColor=Color.DimGray};root.Controls.Add(info,0,2);
        var grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White};foreach(string column in new[]{"时间","提醒","处理"})grid.Columns.Add(column,column);root.Controls.Add(grid,0,3);
        void RefreshHistory(){grid.Rows.Clear();foreach(var entry in notifications.History)grid.Rows.Add(entry.At.ToLocalTime().ToString("MM-dd HH:mm:ss"),entry.Title,entry.Outcome);}
        var save=new Button{Text="保存",AutoSize=true,Anchor=AnchorStyles.Right};root.Controls.Add(save,0,4);
        save.Click+=(_,_)=>{try{notifications.Save(new(completion.Checked,attention.Checked,quota.Checked,sound.Checked,quiet.Checked,start.Value.Hour*60+start.Value.Minute,end.Value.Hour*60+end.Value.Minute,health.Checked));info.Text="已保存";}catch(Exception ex){info.Text=ex.Message;}};
        SettingsWindow.StyleButton(save,true);SettingsWindow.AddTitle(root,"提醒管理");Shown+=(_,_)=>{RefreshHistory();_timer.Start();};_timer.Tick+=(_,_)=>RefreshHistory();SettingsWindow.FitScreen(this);
    }
    protected override void Dispose(bool disposing){if(disposing&&!_disposed){_disposed=true;_timer.Dispose();}base.Dispose(disposing);}
}
