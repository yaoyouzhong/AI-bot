namespace AIBotBridge;
internal sealed record NotificationOptions(bool Completion=true,bool Attention=true,bool Quota=true,bool Sound=true,bool Quiet=false,int QuietStart=1320,int QuietEnd=480)
{
    internal void Validate(){if(QuietStart is <0 or >=1440||QuietEnd is <0 or >=1440||Quiet&&QuietStart==QuietEnd)throw new InvalidOperationException("勿扰开始和结束时间须不同。");}
    internal bool IsQuiet(DateTime local){int minute=local.Hour*60+local.Minute;return Quiet&&(QuietStart<QuietEnd?minute>=QuietStart&&minute<QuietEnd:minute>=QuietStart||minute<QuietEnd);}
}
internal sealed record NotificationEntry(DateTimeOffset At,string Kind,string Title,string Outcome);
internal sealed record NotificationDecision(bool Show,bool Sound);
internal sealed class BridgeNotifications
{
    private NotificationOptions? _options;
    private readonly List<NotificationEntry> _history=[];
    private readonly Dictionary<string,DateTimeOffset> _seen=[];
    private readonly Dictionary<string,DateTimeOffset> _lastShown=[];
    internal string? Error {get;private set;}
    internal NotificationOptions Options {
        get{if(_options is not null)return _options;try{var value=UserPreferenceFile.Read<NotificationOptions>("notifications.json")??new();value.Validate();_options=value;}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or InvalidOperationException){Error="提醒设置未能读取，暂用默认设置："+ex.Message;_options=new();}return _options;}
    }
    internal void Save(NotificationOptions value){value.Validate();UserPreferenceFile.Write("notifications.json",value);_options=value;Error=null;}
    internal NotificationEntry[] History=>_history.AsEnumerable().Reverse().ToArray();
    internal NotificationDecision Evaluate(string kind,string key,string title,DateTimeOffset at) {
        if(kind is not ("completion" or "attention" or "quota"))throw new ArgumentException("Unknown notification kind");
        var options=Options;
        foreach(var old in _seen.Where(p=>at-p.Value>TimeSpan.FromHours(24)).Select(p=>p.Key).ToArray())_seen.Remove(old);
        string identity=kind+":"+key;if(_seen.ContainsKey(identity))return new(false,false);
        if(_seen.Count>=512)_seen.Remove(_seen.MinBy(p=>p.Value).Key);_seen[identity]=at;
        bool enabled=kind switch{"completion"=>options.Completion,"attention"=>options.Attention,_=>options.Quota};
        string outcome=!enabled?"已关闭":options.IsQuiet(at.LocalDateTime)?"勿扰时段":
            _lastShown.TryGetValue(kind,out var previous)&&at-previous<TimeSpan.FromSeconds(kind=="quota"?300:10)?"合并频繁提醒":"已请求通知";
        bool show=outcome=="已请求通知";if(show)_lastShown[kind]=at;
        _history.Add(new(at,kind,title,outcome));if(_history.Count>100)_history.RemoveAt(0);
        return new(show,show&&options.Sound&&kind=="completion");
    }
}
internal sealed class NotificationSettingsForm : Form
{
    private readonly System.Windows.Forms.Timer _timer=new(){Interval=2000};
    private bool _disposed;
    internal NotificationSettingsForm(BridgeNotifications notifications) {
        Text="AI-bot · 电脑提醒";Font=new("Microsoft YaHei UI",9);AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new(96,96);ClientSize=new(600,390);MinimumSize=new(540,330);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(16),ColumnCount=1,RowCount=5};for(int i=0;i<5;i++)root.RowStyles.Add(new(i==3?SizeType.Percent:SizeType.AutoSize,i==3?100:0));root.ColumnStyles.Add(new(SizeType.Percent,100));Controls.Add(root);
        var value=notifications.Options;var checks=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};
        CheckBox Box(string text,bool selected){var box=new CheckBox{Text=text,Checked=selected,AutoSize=true};checks.Controls.Add(box);return box;}
        var completion=Box("Codex 完成",value.Completion);var attention=Box("等待操作",value.Attention);var quota=Box("额度异常",value.Quota);var sound=Box("完成音",value.Sound);root.Controls.Add(checks,0,0);
        var quiet=new CheckBox{Text="勿扰时段",Checked=value.Quiet,AutoSize=true};
        DateTimePicker Time(int minute)=>new(){Format=DateTimePickerFormat.Custom,CustomFormat="HH:mm",ShowUpDown=true,Value=DateTime.Today.AddMinutes(minute),Width=90};
        var start=Time(value.QuietStart);var end=Time(value.QuietEnd);var separator=new Label{Text="—",AutoSize=true};var hours=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};hours.Controls.AddRange([quiet,start,separator,end]);root.Controls.Add(hours,0,1);
        void QuietVisibility(){start.Visible=end.Visible=separator.Visible=quiet.Checked;}quiet.CheckedChanged+=(_,_)=>QuietVisibility();QuietVisibility();
        var info=new Label{Text=notifications.Error??"本次运行",AutoSize=true,Dock=DockStyle.Fill,ForeColor=Color.DimGray};root.Controls.Add(info,0,2);
        var grid=new DataGridView{Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,BackgroundColor=Color.White};foreach(string column in new[]{"时间","提醒","处理"})grid.Columns.Add(column,column);root.Controls.Add(grid,0,3);
        void RefreshHistory(){grid.Rows.Clear();foreach(var entry in notifications.History)grid.Rows.Add(entry.At.ToLocalTime().ToString("MM-dd HH:mm:ss"),entry.Title,entry.Outcome);}
        var save=new Button{Text="保存",AutoSize=true,Anchor=AnchorStyles.Right};root.Controls.Add(save,0,4);
        save.Click+=(_,_)=>{try{notifications.Save(new(completion.Checked,attention.Checked,quota.Checked,sound.Checked,quiet.Checked,start.Value.Hour*60+start.Value.Minute,end.Value.Hour*60+end.Value.Minute));info.Text="已保存 · 本次运行记录";}catch(Exception ex){info.Text=ex.Message;}};
        Shown+=(_,_)=>{RefreshHistory();_timer.Start();};_timer.Tick+=(_,_)=>RefreshHistory();SettingsWindow.FitScreen(this);
    }
    protected override void Dispose(bool disposing){if(disposing&&!_disposed){_disposed=true;_timer.Dispose();}base.Dispose(disposing);}
}
