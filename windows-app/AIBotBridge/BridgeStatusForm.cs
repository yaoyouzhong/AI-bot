namespace AIBotBridge;

internal sealed record BridgeDeviceStatus(string Name,string Status,string Transport,string Firmware,string Id="",bool Enabled=false,ConnectionObservation? Health=null,bool HostWriteOnly=false);
internal sealed record BridgeDataStatus(string Name,string Status,string Updated);
internal sealed record BridgeStatusView(BridgeDeviceStatus[] Devices,BridgeDataStatus[] Data,int Enabled,string? Error)
{
    internal static BridgeStatusView Capture(DeviceRegistry registry,Func<RegisteredDevice,DeviceView> view,
        StatusSnapshot snapshot,DeviceDataDemand demand,string? error,Func<RegisteredDevice,ConnectionObservation>? health=null)
    {
        var devices=registry.Devices.Select(d=>{var v=view(d);return new BridgeDeviceStatus(d.Name,d.Enabled?v.Status:"已停用",v.Transport??"—",v.Firmware,d.Id,d.Enabled,health?.Invoke(d),d.Kind==HardwareKind.Esp8266&&v.Transport=="USB");}).ToArray();
        var data=new List<BridgeDataStatus>();
        void Add(string name,string source,DateTimeOffset? at,bool stale,bool selected=true)=>data.Add(new(name,
            !demand.Sources.Contains(source)||!selected?"未启用":at is null?"暂无数据":stale?"旧缓存":"数据可用",at?.ToLocalTime().ToString("MM-dd HH:mm:ss")??"—"));
        Add("天气","weather",snapshot.Weather?.UpdatedAt,snapshot.Weather?.Stale??true);
        Add("股票","stocks",snapshot.Stocks?.UpdatedAt,snapshot.Stocks?.Stale??true);
        Add("Codex 额度","quotas",snapshot.Quotas?.Codex?.UpdatedAt,snapshot.Quotas?.Codex?.Stale??true);
        Add("Claude 额度","quotas",snapshot.Quotas?.Claude?.UpdatedAt,snapshot.Quotas?.Claude?.Stale??true);
        foreach(var (id,name,q) in new (string,string,DomesticProviderQuotaSnapshot?)[]{
            ("qwen","通义千问",snapshot.DomesticQuotas?.Alibaba),("kimi","Kimi",snapshot.DomesticQuotas?.Kimi),
            ("minimax","MiniMax",snapshot.DomesticQuotas?.MiniMax),("deepseek","DeepSeek",snapshot.DomesticQuotas?.DeepSeek),
            ("zhipu","智谱",snapshot.DomesticQuotas?.Zhipu),("stepfun","阶跃星辰",snapshot.DomesticQuotas?.StepFun),
            ("baidu","百度",snapshot.DomesticQuotas?.Baidu),("xiaomi","小米",snapshot.DomesticQuotas?.Xiaomi)})
            if(demand.Providers.Contains(id))Add(name+" 额度","quotas",q?.UpdatedAt,q?.Stale??true);
        return new(devices,data.ToArray(),registry.Devices.Count(d=>d.Enabled),error);
    }
}

// User-facing status; raw snapshots remain available through local diagnostics.
internal sealed class BridgeStatusForm : Form
{
    private readonly Func<BridgeStatusView> _capture;
    private readonly Label _summary=new(){AutoSize=true,ForeColor=Color.DimGray};
    private readonly Label _updated=new(){AutoSize=true,ForeColor=Color.DimGray};
    private readonly Label _error=new(){AutoSize=true,ForeColor=Color.Firebrick};
    private readonly DataGridView _devices=Grid();
    private readonly DataGridView _data=Grid();
    private readonly TableLayoutPanel _layout;
    private readonly Label _health=new(){AutoSize=true,Dock=DockStyle.Fill,ForeColor=Color.DimGray};
    private readonly Button _reconnect=new(){Text="重新连接",AutoSize=true};
    private BridgeDeviceStatus[] _rows=[];
    private bool _busy;
    private readonly bool _canReconnect;
    private readonly System.Windows.Forms.Timer _timer=new(){Interval=2000};
    internal BridgeStatusForm(Func<BridgeStatusView> capture,Func<string,Task>? reconnect=null,Action<string>? export=null)
    {
        _capture=capture;_canReconnect=reconnect is not null;Text="AI-bot · 服务状态";Font=new Font("Microsoft YaHei UI",9F);
        AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new(720,540);MinimumSize=new(640,460);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.White;
        var root=_layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=9,Padding=new Padding(16)};
        root.ColumnStyles.Add(new(SizeType.Percent,100));
        foreach(var style in new[]{new RowStyle(SizeType.AutoSize),new RowStyle(SizeType.AutoSize),new RowStyle(SizeType.Percent,33),new RowStyle(SizeType.AutoSize),new RowStyle(SizeType.Percent,67),new RowStyle(SizeType.AutoSize),new RowStyle(SizeType.AutoSize)})root.RowStyles.Add(style);
        root.Controls.Add(_summary,0,0);root.Controls.Add(Title("设备连接"),0,1);root.Controls.Add(_devices,0,2);
        root.Controls.Add(Title("数据更新"),0,3);root.Controls.Add(_data,0,4);root.Controls.Add(_error,0,5);root.Controls.Add(_updated,0,6);Controls.Add(root);
        root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.AutoSize));root.Controls.Add(_health,0,7);
        var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,Margin=new Padding(0,8,0,0)};
        var exportButton=new Button{Text="导出诊断包…",AutoSize=true,Enabled=export is not null};
        actions.Controls.Add(_reconnect);actions.Controls.Add(exportButton);root.Controls.Add(actions,0,8);
        _reconnect.Click+=async(_,_)=>{
            var row=Selected();if(_busy||row is null||reconnect is null)return;
            _busy=true;_reconnect.Enabled=false;
            try{await reconnect(row.Id);_error.Text="已重启服务，等待连接。";}
            catch(Exception ex){_error.Text=ex.Message;}
            finally{_busy=false;if(!IsDisposed)UpdateHealth(reconnect is not null);}
        };
        exportButton.Click+=(_,_)=>{
            using var pick=new SaveFileDialog{Filter="诊断包 (*.zip)|*.zip",FileName="AI-bot-diagnostics-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".zip"};
            if(pick.ShowDialog(this)!=DialogResult.OK)return;
            try{export?.Invoke(pick.FileName);_error.Text="诊断包已保存。";}
            catch(Exception ex){_error.Text="导出失败："+ex.Message;}
        };
        _devices.SelectionChanged+=(_,_)=>UpdateHealth(reconnect is not null);
        AddColumns(_devices,["设备","状态","当前通道","固件"],[32,18,18,32]);
        AddColumns(_data,["数据来源","状态","最近成功更新"],[32,24,44]);
        root.SizeChanged+=(_,_)=>{foreach(var label in new[]{_summary,_error,_updated,_health})label.MaximumSize=new(Math.Max(100,root.ClientSize.Width-root.Padding.Horizontal),0);};
        _timer.Tick+=(_,_)=>RefreshStatus();Shown+=(_,_)=>{RefreshStatus();_timer.Start();};SettingsWindow.FitScreen(this);
    }
    internal void RefreshStatus()
    {
        try{
            var snapshot=_capture();_summary.Text=$"已启用 {snapshot.Enabled} / {snapshot.Devices.Length} 台";
            _rows=snapshot.Devices;
            SetRows(_devices,snapshot.Devices.Length==0?[["尚未添加设备","—","—","—"]]:snapshot.Devices.Select(d=>new[]{d.Name,d.Status,d.Transport,d.Firmware}).ToArray());
            _layout.RowStyles[2].SizeType=SizeType.Absolute;_layout.RowStyles[2].Height=Math.Min(ClientSize.Height/3,_devices.ColumnHeadersHeight+_devices.Rows.Cast<DataGridViewRow>().Sum(r=>r.Height)+8);
            SetRows(_data,snapshot.Data.Select(d=>new[]{d.Name,d.Status,d.Updated}).ToArray());
            _error.Text=snapshot.Error is null?"":"连接异常："+snapshot.Error;
            _updated.Text=$"更新于 {DateTime.Now:HH:mm:ss}";
            UpdateHealth(_canReconnect);
        }catch(Exception ex){_error.Text="状态读取失败："+ex.Message;_updated.Text="当前显示为上次读取结果，将自动重试。";}
    }
    private BridgeDeviceStatus? Selected(){int index=_devices.CurrentCell?.RowIndex??-1;return index>=0&&index<_rows.Length?_rows[index]:null;}
    private void UpdateHealth(bool canReconnect) {
        var row=Selected();_reconnect.Enabled=canReconnect&&!_busy&&row?.Enabled==true;
        _health.Text=row?.Health is {} h?$"{(row.HostWriteOnly?"最近发送":"最近通信")} {h.LastCommunication?.ToString("HH:mm:ss")??"—"} · 本次恢复 {h.Recoveries} · 重连 {h.ManualRestarts}"+
            (h.LastDisconnected is {} at?$"\n中断 {at:MM-dd HH:mm:ss} · {h.Reason}":""):"";
    }
    private static Label Title(string text)=>new(){Text=text,AutoSize=true,Margin=new Padding(0,12,0,6)};
    private static DataGridView Grid()=>new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize};
    private static void AddColumns(DataGridView grid,string[] names,int[] widths)
    {
        grid.DefaultCellStyle.Padding=new Padding(4,5,4,5);grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;
        grid.DefaultCellStyle.SelectionBackColor=Color.FromArgb(244,247,250);grid.DefaultCellStyle.SelectionForeColor=Color.Black;
        for(int i=0;i<names.Length;i++)grid.Columns.Add(new DataGridViewTextBoxColumn{HeaderText=names[i],FillWeight=widths[i],SortMode=DataGridViewColumnSortMode.NotSortable});
    }
    private static void SetRows(DataGridView grid,string[][] values)
    {
        while(grid.Rows.Count>values.Length)grid.Rows.RemoveAt(grid.Rows.Count-1);
        while(grid.Rows.Count<values.Length)grid.Rows.Add();
        for(int r=0;r<values.Length;r++)for(int c=0;c<values[r].Length;c++)if(!Equals(grid[c,r].Value,values[r][c]))grid[c,r].Value=values[r][c];
        foreach(DataGridViewRow row in grid.Rows){string status=row.Cells[1].Value?.ToString()??"";row.Cells[1].Style.ForeColor=status is "在线" or "数据可用"?Color.FromArgb(24,123,72):status is "旧缓存" or "连接异常"?Color.DarkOrange:Color.DimGray;row.Cells[1].ToolTipText=status=="旧缓存"?"尚未取得最新数据，保留上次成功结果。":"";}
    }
    protected override void Dispose(bool disposing){if(disposing){_timer.Stop();_timer.Dispose();}base.Dispose(disposing);}
}
