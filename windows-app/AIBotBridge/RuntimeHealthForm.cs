namespace AIBotBridge;

internal sealed class RuntimeHealthForm : Form
{
    private readonly System.Windows.Forms.Timer _timer=new(){Interval=2000};
    private readonly ToolTip _tips=new();
    internal RuntimeHealthForm(RuntimeHealthService service,Func<RuntimeHealthSnapshot?>? capture=null) {
        capture??=()=>service.Snapshot;
        Text="AI-bot · 运行监控";Font=SettingsWindow.BodyFont();AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new(96,96);
        ClientSize=new(880,650);MinimumSize=new(680,500);
        var tabs=new TabControl{Dock=DockStyle.Fill,Name="monitor-tabs"};Controls.Add(tabs);
        var statusPage=new TabPage("监控状态");var settingsPage=new TabPage("检测设置");tabs.TabPages.AddRange([statusPage,settingsPage]);
        var overview=new RuntimeHealthOverview(group=>{using var detail=new RuntimeHealthDetailForm(group,capture(),service.Metrics);detail.ShowDialog(this);});statusPage.Controls.Add(overview);
        var options=service.Options;
        var settingsScroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true};settingsPage.Controls.Add(settingsScroll);
        var settings=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new(20),ColumnCount=1,RowCount=3};
        settings.ColumnStyles.Add(new(SizeType.Percent,100));for(int i=0;i<3;i++)settings.RowStyles.Add(new(SizeType.AutoSize));settingsScroll.Controls.Add(settings);
        var toggles=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2,RowCount=2,Margin=new(0,8,0,8)};
        for(int i=0;i<2;i++){toggles.ColumnStyles.Add(new(SizeType.Percent,50));toggles.RowStyles.Add(new(SizeType.AutoSize));}
        CheckBox Check(string caption,bool value,int column,int row){var box=new CheckBox{Text=caption,Checked=value,AutoSize=true,Anchor=AnchorStyles.Left,Margin=new(0,6,12,6)};toggles.Controls.Add(box,column,row);return box;}
        var enabled=Check("启用监控",options.Enabled,0,0);var network=Check("检测 VPN / PAC",options.Network,1,0);
        var proxy=Check("关键域名走代理",options.ExpectChatGptProxy,0,1);var speed=Check("小流量测速",options.Throughput,1,1);settings.Controls.Add(toggles,0,0);
        _tips.SetToolTip(proxy,"检查 ChatGPT 和资源服务是否按预期走代理；使用直连或 TUN 时可取消。\n此选项只用于检测，不修改系统代理。");
        var thresholds=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2,RowCount=5,Margin=new(0,12,0,12)};
        thresholds.ColumnStyles.Add(new(SizeType.Percent,100));thresholds.ColumnStyles.Add(new(SizeType.AutoSize));int thresholdRow=0;
        NumericUpDown Number(string caption,int value,int min,int max) {
            thresholds.RowStyles.Add(new(SizeType.AutoSize));
            thresholds.Controls.Add(new Label{Text=caption,AutoSize=true,Anchor=AnchorStyles.Left,Margin=new(0,8,12,8)},0,thresholdRow);
            var box=new NumericUpDown{Minimum=min,Maximum=max,Value=value,Width=110,Margin=new(0,6,0,6),Anchor=AnchorStyles.Right};
            thresholds.Controls.Add(box,1,thresholdRow++);return box;
        }
        var interval=Number("检测间隔 / 秒",options.ProbeSeconds,15,300);var slow=Number("慢响应 / ms",options.SlowMilliseconds,500,15000);
        var cpu=Number("CPU / %",options.CpuPercent,50,100);var memory=Number("内存 / %",options.MemoryPercent,50,100);
        var sustained=Number("持续 / 秒",options.SustainedSeconds,10,300);settings.Controls.Add(thresholds,0,1);
        var footer=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,Margin=new(0,12,0,0)};
        var save=new Button{Text="保存设置",AutoSize=true};SettingsWindow.StyleButton(save,true);footer.Controls.Add(save);
        var result=new Label{AutoSize=true,MaximumSize=new(550,0),Text="小流量测速：每 5 分钟读取 128 KiB。",Margin=new(12,8,0,0)};footer.Controls.Add(result);settings.Controls.Add(footer,0,2);
        save.Click+=(_,_)=>{try{var value=new RuntimeHealthOptions(enabled.Checked,network.Checked,proxy.Checked,(int)interval.Value,(int)slow.Value,(int)cpu.Value,(int)memory.Value,(int)sustained.Value,speed.Checked);
            value.Validate();UserPreferenceFile.Write("runtime-health.json",value);service.Reload();if(service.Options!=value)throw new InvalidOperationException("监控设置尚未确认，请重试。");result.Text="已保存，重新检测中。";}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or InvalidOperationException){result.Text=ex.Message;}};
        void Refresh()=>overview.RefreshSnapshot(capture(),service.Metrics);
        _timer.Tick+=(_,_)=>Refresh();Shown+=(_,_)=>{Refresh();_timer.Start();};SettingsWindow.FitScreen(this);
    }
    protected override void Dispose(bool disposing){if(disposing){_timer.Dispose();_tips.Dispose();}base.Dispose(disposing);}
}
