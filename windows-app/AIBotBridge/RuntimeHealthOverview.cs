namespace AIBotBridge;

internal sealed class RuntimeHealthOverview : UserControl
{
    private readonly Label _notice,_advice,_vpn,_pac,_latency,_system,_cpu,_memory,_pressure;
    private readonly Panel _cpuBar,_memoryBar;
    private double _cpuValue,_memoryValue;
    private Color _cpuColor=Color.SteelBlue,_memoryColor=Color.SeaGreen;
    private string _detailGroup="network";
    internal RuntimeHealthOverview(Action<string> details) {
        Font=SettingsWindow.BodyFont();Dock=DockStyle.Fill;BackColor=Color.White;
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(20),ColumnCount=1,RowCount=3};
        root.ColumnStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));Controls.Add(root);
        _notice=TextLabel("等待检测",true);_notice.Margin=new(0,0,0,14);root.Controls.Add(_notice,0,0);
        var cards=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};cards.ColumnStyles.Add(new(SizeType.Percent,50));cards.ColumnStyles.Add(new(SizeType.Percent,50));cards.RowStyles.Add(new(SizeType.Percent,100));root.Controls.Add(cards,0,1);
        TableLayoutPanel Card(int column) {
            var card=new TableLayoutPanel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(246,248,250),Padding=new(14),ColumnCount=1,
                Margin=column==0?new(0,0,8,0):new(8,0,0,0)};card.ColumnStyles.Add(new(SizeType.Percent,100));cards.Controls.Add(card,column,0);return card;
        }
        TableLayoutPanel Pair(string caption,out Label value) {
            var row=new TableLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,ColumnCount=2,RowCount=1,Margin=new(0,6,0,6)};
            row.ColumnStyles.Add(new(SizeType.Percent,100));row.ColumnStyles.Add(new(SizeType.AutoSize));row.Controls.Add(TextLabel(caption),0,0);
            value=TextLabel("--");value.Anchor=AnchorStyles.Right;row.Controls.Add(value,1,0);return row;
        }
        var network=Card(0);network.RowCount=5;
        network.RowStyles.Add(new(SizeType.AutoSize));network.RowStyles.Add(new(SizeType.AutoSize));network.RowStyles.Add(new(SizeType.Percent,100));network.RowStyles.Add(new(SizeType.AutoSize));network.RowStyles.Add(new(SizeType.AutoSize));
        network.Controls.Add(Pair("VPN 响应",out _vpn),0,0);
        _latency=TextLabel("-- ms",true);_latency.Font=new(Font.FontFamily,Font.Size+8,FontStyle.Regular);_latency.Margin=new(0,12,0,8);network.Controls.Add(_latency,0,1);
        network.Controls.Add(Pair("PAC 路由",out _pac),0,3);
        var networkDetails=new Button{Text="网络详情",AutoSize=true,Anchor=AnchorStyles.Left,Margin=new(0,12,0,0)};networkDetails.Click+=(_,_)=>details("network");SettingsWindow.StyleButton(networkDetails,false);network.Controls.Add(networkDetails,0,4);
        var system=Card(1);system.RowCount=8;
        for(int i=0;i<8;i++)system.RowStyles.Add(new(i==6?SizeType.Percent:SizeType.AutoSize,i==6?100:0));
        system.Controls.Add(Pair("电脑负载",out _system),0,0);system.Controls.Add(Pair("CPU",out _cpu),0,1);
        Panel Bar()=>new(){Dock=DockStyle.Fill,Height=10,Margin=new(0,0,0,8),BackColor=Color.FromArgb(219,225,230)};
        _cpuBar=Bar();_memoryBar=Bar();system.Controls.Add(_cpuBar,0,2);system.Controls.Add(Pair("内存",out _memory),0,3);system.Controls.Add(_memoryBar,0,4);
        _pressure=TextLabel("等待 -- · 磁盘 --");_pressure.ForeColor=Color.DimGray;_pressure.Margin=new(0,10,0,6);system.Controls.Add(_pressure,0,5);
        _cpuBar.Paint+=(_,e)=>{using var brush=new SolidBrush(_cpuColor);e.Graphics.FillRectangle(brush,0,0,(int)(_cpuBar.Width*_cpuValue/100),_cpuBar.Height);};
        _memoryBar.Paint+=(_,e)=>{using var brush=new SolidBrush(_memoryColor);e.Graphics.FillRectangle(brush,0,0,(int)(_memoryBar.Width*_memoryValue/100),_memoryBar.Height);};
        var computerDetails=new Button{Text="电脑详情",AutoSize=true,Anchor=AnchorStyles.Left,Margin=new(0,12,0,0)};computerDetails.Click+=(_,_)=>details("system");SettingsWindow.StyleButton(computerDetails,false);system.Controls.Add(computerDetails,0,7);
        _advice=TextLabel("");_advice.ForeColor=Color.DimGray;_advice.Margin=new(0,16,0,0);root.Controls.Add(_advice,0,2);
        _notice.Cursor=Cursors.Hand;_notice.Click+=(_,_)=>details(_detailGroup);
    }
    private Label TextLabel(string text,bool bold=false)=>new(){Text=text,AutoSize=true,Margin=new(0),Font=bold?new Font(Font,FontStyle.Bold):Font,Anchor=AnchorStyles.Left};
    private static Color StateColor(string? state)=>state=="error"?Color.Firebrick:state=="warning"?Color.DarkOrange:state=="ok"?Color.SeaGreen:Color.DimGray;
    private static string StateText(string? state)=>state switch{"ok"=>"正常","warning"=>"偏慢","error"=>"异常","observing"=>"观察中","disabled"=>"未启用",_=>"待确认"};
    internal void RefreshSnapshot(RuntimeHealthSnapshot? snapshot,SystemMetricsSnapshot? metrics) {
        bool fresh=snapshot is not null&&DateTimeOffset.UtcNow-snapshot.UpdatedAt<TimeSpan.FromSeconds(10);
        RuntimeHealthCheck? Check(string key)=>fresh?snapshot!.Checks.FirstOrDefault(c=>c.Key==key):null;
        void State(Label label,RuntimeHealthCheck? check){label.Text=StateText(check?.State);label.ForeColor=StateColor(check?.State);}
        var vpn=Check("vpn");var pac=Check("pac");State(_vpn,vpn);State(_pac,pac);
        _latency.Text=vpn?.LatencyMs is {} ms?$"{ms:0} ms":"-- ms";
        var states=new[]{Check("cpu"),Check("memory"),Check("queue"),Check("disk")};
        bool high=states.Any(c=>c?.State is "warning" or "error"),known=states.All(c=>c?.State=="ok");
        _system.Text=high?"繁忙":known?"正常":"待确认";_system.ForeColor=high?Color.DarkOrange:known?Color.SeaGreen:Color.DimGray;
        bool values=fresh&&metrics is not null&&DateTimeOffset.UtcNow-metrics.UpdatedAt<TimeSpan.FromSeconds(10);
        _cpu.Text=values?$"{metrics!.CpuPercent:0}%":"--";_memory.Text=values?$"{metrics!.MemoryPercent:0}%":"--";
        _cpuValue=values?Math.Clamp(metrics!.CpuPercent,0,100):0;_memoryValue=values?Math.Clamp(metrics!.MemoryPercent,0,100):0;
        _cpuColor=Check("cpu")?.State=="warning"?Color.DarkOrange:Color.SteelBlue;_memoryColor=Check("memory")?.State=="warning"?Color.DarkOrange:Color.SeaGreen;
        _cpuBar.Invalidate();_memoryBar.Invalidate();
        var pressure=fresh?snapshot?.Pressure:null;string Number(double? n,string unit="")=>n.HasValue?n.Value.ToString("0")+unit:"--";
        _pressure.Text=$"等待 {Number(pressure?.ProcessorQueue)} · 磁盘 {Number(pressure?.DiskLatencyMs," ms")}";
        var alert=fresh?snapshot?.Alerts.FirstOrDefault():null;
        _notice.Text=alert?.Title??(fresh&&known&&vpn?.State=="ok"&&pac?.State is "ok" or "disabled"?"运行正常":snapshot is null?"等待检测":"状态待确认");
        _notice.ForeColor=alert is null?Color.DimGray:StateColor(alert.State);
        _advice.Text=alert?.ShortAdvice??(snapshot is null?"启用系统数据后开始监控。":$"数据更新 {snapshot.UpdatedAt.ToLocalTime():HH:mm:ss}");
        _detailGroup=alert?.Key is "cpu" or "memory" or "queue" or "disk"?"system":"network";
        if(!fresh&&snapshot is not null){_notice.Text="监控已中断";_advice.Text="检查桥接程序。";}
    }
}

internal sealed class RuntimeHealthDetailForm : Form
{
    internal RuntimeHealthDetailForm(string group,RuntimeHealthSnapshot? snapshot,SystemMetricsSnapshot? metrics) {
        Text=group=="system"?"AI-bot · 电脑详情":"AI-bot · 网络详情";Font=SettingsWindow.BodyFont();AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new(96,96);ClientSize=new(800,580);MinimumSize=new(600,420);
        var body=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.White,Margin=new(20)};
        var panel=new Panel{Dock=DockStyle.Fill,Padding=new(24),BackColor=Color.White};panel.Controls.Add(body);Controls.Add(panel);
        var keys=group=="system"?new[]{"cpu","memory","queue","disk"}:new[]{"vpn","pac"};
        body.Text=snapshot is null?"等待监控数据。":string.Join("\r\n\r\n",snapshot.Checks.Where(c=>keys.Contains(c.Key)).Select(c=>$"{c.Label} · {c.Summary}\r\n{c.Detail}"+(c.Advice.Length==0?"":"\r\n"+c.Advice)));
        if(group=="system"&&metrics is not null)body.AppendText($"\r\n\r\n整机流量\r\n上传 {metrics.UploadBytesPerSecond/1024.0:0.#} KB/s · 下载 {metrics.DownloadBytesPerSecond/1024.0:0.#} KB/s");
        if(group=="network"&&snapshot?.ThroughputMbps is {} speed)body.AppendText($"\r\n\r\n小流量吞吐 {speed:0.##} Mbps · {snapshot.ThroughputAt?.ToLocalTime():HH:mm:ss}\r\n此采样不代表节点最大带宽。");
        body.SelectionStart=0;SettingsWindow.FitScreen(this);
    }
}
