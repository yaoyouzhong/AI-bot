using System.Diagnostics;
namespace AIBotBridge;
internal sealed record UpdateDevice(string Id,string Name,string Version,string Status,bool Enabled,string Action,string Requirement,bool Online=false)
{
    internal string Component=>Action=="bridge"?"bridge":Action=="upgrade-tab5"?"tab5":"esp8266";
    internal static UpdateDevice From(RegisteredDevice device,DeviceView view)=>new(device.Id,device.Name,view.Firmware,view.Status,device.Enabled,
        device.Kind==HardwareKind.Tab5?"upgrade-tab5":"flash",device.Kind==HardwareKind.Tab5?"在 TAB5 上确认安装":"连接 USB，先备份再升级",view.Online);
}
internal sealed record UpdatePreferences(bool Automatic=true,string[]? Notified=null);
internal sealed class UpdateCenterForm : Form
{
    private readonly CancellationTokenSource _stop=new();
    private readonly System.Windows.Forms.Timer _timer=new(){Interval=2000};
    private CancellationTokenSource? _operation;
    private bool _disposed,_busy;
    internal UpdateCenterForm(Func<UpdateDevice[]> capture,Func<UpdateDevice,PreparedUpdate,Task> install,UpdateService service) {
        SuspendLayout();
        var ink=Color.FromArgb(32,43,59);var muted=Color.FromArgb(88,101,119);var blue=Color.FromArgb(34,109,215);
        Text="AI-bot · 软件与固件更新";Font=new("Microsoft YaHei UI",9.5f);ForeColor=ink;BackColor=Color.White;AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new(850,550);MinimumSize=new(760,0);AutoScroll=true;
        var root=new TableLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,Padding=new(24),ColumnCount=1,RowCount=7};Controls.Add(root);
        root.ColumnStyles.Add(new(SizeType.Percent,100));for(int i=0;i<7;i++)root.RowStyles.Add(new(SizeType.AutoSize));
        var heading=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=1,Margin=new(0,0,0,18)};
        heading.ColumnStyles.Add(new(SizeType.Percent,100));
        heading.Controls.Add(new Label{Text="软件与固件更新",Font=new(Font.FontFamily,17,FontStyle.Bold),AutoSize=true,Margin=new(0,0,0,6)},0,0);
        var subtitle=new Label{Text="电脑软件、TAB5 和小屏分别更新，选择一项查看详情。",ForeColor=muted,AutoSize=true,Dock=DockStyle.Fill,Margin=Padding.Empty};heading.Controls.Add(subtitle,0,1);root.Controls.Add(heading,0,0);
        var tools=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,Margin=new(0,0,0,14)};var check=new Button{Text="检查更新",Name="check",Margin=new(0,0,16,0)};SettingsWindow.StyleButton(check);
        var automatic=new CheckBox{Text="自动检查并提醒",AutoSize=true,Checked=UpdateReminder.Read().Automatic,Margin=new(0,7,0,0)};
        tools.Controls.AddRange([check,automatic]);root.Controls.Add(tools,0,1);
        var grid=new DataGridView{Name="updates",Dock=DockStyle.Top,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,AllowUserToResizeColumns=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal,ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None,EnableHeadersVisualStyles=false,ColumnHeadersHeight=38,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,GridColor=Color.FromArgb(231,236,242),Margin=Padding.Empty};
        grid.RowTemplate.Height=50;
        grid.DefaultCellStyle=new(){ForeColor=ink,BackColor=Color.White,SelectionBackColor=Color.FromArgb(233,242,255),SelectionForeColor=ink,Padding=new(12,0,8,0),WrapMode=DataGridViewTriState.False};
        grid.ColumnHeadersDefaultCellStyle=new(){ForeColor=muted,BackColor=Color.FromArgb(246,248,251),SelectionBackColor=Color.FromArgb(246,248,251),Padding=new(12,0,8,0),WrapMode=DataGridViewTriState.False};
        foreach(string text in new[]{"软件 / 设备","当前版本","最新发布","状态"})grid.Columns.Add(text,text);
        float[] weights=[33,20,20,27];for(int i=0;i<grid.Columns.Count;i++){grid.Columns[i].SortMode=DataGridViewColumnSortMode.NotSortable;grid.Columns[i].FillWeight=weights[i];}
        grid.Columns[0].DefaultCellStyle.Font=new(Font,FontStyle.Bold);root.Controls.Add(grid,0,2);
        var detail=new TableLayoutPanel{Name="update-actions",AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,ColumnCount=1,Padding=new(16),Margin=new(0,14,0,0),BackColor=Color.FromArgb(246,248,251)};detail.ColumnStyles.Add(new(SizeType.Percent,100));
        var requirement=new Label{Name="requirement",AutoSize=true,Dock=DockStyle.Fill,ForeColor=muted,Margin=Padding.Empty};detail.Controls.Add(requirement,0,0);
        var actions=new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Fill,Margin=new(0,12,0,0)};
        var download=new Button{Name="download",Text="下载并升级",Margin=new(0,0,12,0)};SettingsWindow.StyleButton(download,true);
        var notes=new LinkLabel{Name="release-notes",Text="查看更新说明",AutoSize=true,LinkColor=blue,Margin=new(0,8,16,0)};
        var cancel=new Button{Text="取消下载",Enabled=false,Visible=false,Margin=Padding.Empty};SettingsWindow.StyleButton(cancel);
        actions.Controls.AddRange([download,notes,cancel]);detail.Controls.Add(actions,0,1);root.Controls.Add(detail,0,3);
        var state=new Label{Name="state",AutoSize=true,Dock=DockStyle.Fill,ForeColor=muted,Margin=new(0,12,0,8)};root.Controls.Add(state,0,4);
        var progress=new ProgressBar{Dock=DockStyle.Fill,Visible=false,Height=8,Margin=new(0,0,0,10)};root.Controls.Add(progress,0,5);
        var footer=new TableLayoutPanel{Name="update-footer",AutoSize=true,Dock=DockStyle.Fill,ColumnCount=2,Margin=Padding.Empty};footer.ColumnStyles.Add(new(SizeType.Percent,50));footer.ColumnStyles.Add(new(SizeType.Percent,50));
        var galleries=new LinkLabel{Text="屏保图库…",AutoSize=true,LinkColor=blue,Margin=Padding.Empty};galleries.LinkClicked+=(_,_)=>{using var form=new GalleryPackForm();form.ShowDialog(this);};
        var site=new LinkLabel{Text="完整下载与安装说明",AutoSize=true,LinkColor=blue,Anchor=AnchorStyles.Top|AnchorStyles.Right,Margin=Padding.Empty};footer.Controls.Add(galleries,0,0);footer.Controls.Add(site,1,0);root.Controls.Add(footer,0,6);
        bool fitting=false,fitPending=false;
        void FitContentHeight(){
            if(fitting||IsDisposed||!Visible)return;fitting=true;
            try {
                int wanted=footer.Bottom+root.Padding.Bottom+Height-ClientSize.Height;
                int target=Math.Min(wanted,Screen.FromControl(this).WorkingArea.Height-32);
                if(MinimumSize.Height!=target)MinimumSize=new(MinimumSize.Width,target);
                if(Height!=target)Height=target;
            }finally{fitting=false;}
        }
        root.Layout+=(_,_)=>{if(fitPending||fitting||!IsHandleCreated||IsDisposed)return;fitPending=true;BeginInvoke((Action)(()=>{fitPending=false;FitContentHeight();}));};
        // Auto-sized text follows the viewport, including long device names and errors.
        void FitText(){subtitle.MaximumSize=new(Math.Max(100,heading.ClientSize.Width),0);requirement.MaximumSize=new(Math.Max(100,detail.ClientSize.Width-detail.Padding.Horizontal),0);state.MaximumSize=new(Math.Max(100,root.ClientSize.Width-root.Padding.Horizontal),0);}
        root.SizeChanged+=(_,_)=>FitText();detail.SizeChanged+=(_,_)=>FitText();
        UpdateDevice[] rows=[];bool refreshing=false;
        UpdateDevice? Selected()=>grid.CurrentCell is {} c&&c.RowIndex<rows.Length?rows[c.RowIndex]:null;
        ComponentUpdate? Release(UpdateDevice? d)=>d is not null&&service.Available.TryGetValue(d.Component,out var u)?u:null;
        void Buttons(){
            var d=Selected();var u=Release(d);var blocked=d is null?null:UpdateService.PreparationBlocked(d,u);
            bool retained=blocked is "已是最新版本" or "当前版本较新，保留现有版本";
            download.Visible=u is not null&&!retained;
            download.Enabled=!_busy&&d is not null&&blocked is null;download.Text=d?.Component=="bridge"?"下载并安装":d is not null&&UpdateService.IsLegacyEsp(d)?"手动准备小屏升级":"下载并准备升级";
            notes.Text="查看全部更新说明";notes.Enabled=true;
            requirement.Visible=!retained;
            actions.Margin=new(0,retained?0:12,0,0);
            requirement.Text=d is null?"在上方列表选择一项，查看更新状态和操作。":blocked is not null?blocked:
                UpdateService.IsLegacyEsp(d)?"旧固件未提供版本号，无法比较版本。连接 USB 后先完整备份，再手动确认刷机。":d.Component switch {
                    "bridge"=>"下载并校验安装包后，打开安装程序完成更新。",
                    "tab5"=>"下载并校验固件后，打开升级工具；仍需在 TAB5 上确认安装。",
                    _=>"需要通过 USB 升级。打开刷机工具后，先完整备份，再确认刷机。"};
        }
        string Summary(UpdateDevice d,ComponentUpdate? u,string? blocked)=>blocked switch {
            null=>UpdateService.IsLegacyEsp(d)?"可手动升级":"有更新",
            "已是最新版本"=>"已是最新",
            "当前版本较新，保留现有版本"=>"版本较新",
            "设备未启用"=>"未启用",
            "连接设备后确认运行版本"=>"版本待确认",
            "请先连接小屏"=>"等待连接",
            _=>u is null?(service.CheckedAt is null?"待检查":"暂无更新信息"):"暂不可升级"};
        void RefreshRows(){
            if(refreshing)return;
            refreshing=true;
            try {
                string? id=Selected()?.Id;var next=capture();
                // Keep the selected cell stable during periodic status refreshes.
                // Updating an auto-sized action button while rebuilding rows can re-enter grid layout.
                if(!rows.Select(d=>d.Id).SequenceEqual(next.Select(d=>d.Id))){
                    grid.Rows.Clear();rows=next;
                    foreach(var d in rows)grid.Rows.Add(d.Name,d.Version,"","");
                    int index=Array.FindIndex(rows,d=>d.Id==id);if(index>=0)grid.CurrentCell=grid[0,index];
                }else rows=next;
                grid.Height=grid.ColumnHeadersHeight+1+Math.Min(5,rows.Length)*grid.RowTemplate.Height;
                for(int i=0;i<rows.Length;i++){
                    var d=rows[i];var u=Release(d);var blocked=UpdateService.PreparationBlocked(d,u);string version=d.Version==EspFirmwareVersion.Legacy?"未上报":d.Version==EspFirmwareVersion.Invalid?"上报无效":d.Version;object[] values=[d.Name,version,u?.Version??"待查询",Summary(d,u,blocked)];
                    for(int j=0;j<values.Length;j++)if(!Equals(grid[j,i].Value,values[j]))grid[j,i].Value=values[j];
                    grid[0,i].ToolTipText=d.Name;grid[1,i].ToolTipText=d.Version;
                    var color=blocked=="已是最新版本"?Color.FromArgb(29,120,79):blocked is null?blue:muted;
                    grid[3,i].Style.ForeColor=color;grid[3,i].Style.SelectionForeColor=color;
                }
            }finally{refreshing=false;}
            Buttons();
        }
        void CatalogChanged(){if(_disposed||IsDisposed)return;RefreshRows();if(!_busy)state.Text=service.Error is {} error?"检查未完成，保留上次结果："+error:service.CheckedAt is {} time?"已检查 · "+time.ToLocalTime().ToString("MM-dd HH:mm"):"点击检查更新；下载不会自动开始刷写。";}
        service.Changed+=CatalogChanged;FormClosed+=(_,_)=>service.Changed-=CatalogChanged;
        automatic.CheckedChanged+=(_,_)=>{try{UserPreferenceFile.Write("updates.json",UpdateReminder.Read() with {Automatic=automatic.Checked});}catch(Exception ex){state.Text="提醒设置未保存："+ex.Message;}};
        grid.CurrentCellChanged+=(_,_)=>{if(!refreshing&&IsHandleCreated)BeginInvoke((Action)(()=>{if(!IsDisposed)Buttons();}));};
        check.Click+=async(_,_)=>{
            _busy=true;check.Enabled=false;Buttons();state.Text="正在查询适用于电脑和设备的正式更新…";
            try{await service.CheckAsync(_stop.Token);}
            catch(Exception ex){if(!IsDisposed)state.Text="检查未完成："+ex.Message;}
            finally{_busy=false;if(!IsDisposed){check.Enabled=true;CatalogChanged();}}
        };
        notes.Click+=(_,_)=>{using var dialog=new UpdateNotesForm(service.Available,capture(),Selected()?.Component??"bridge");dialog.ShowDialog(this);};
        cancel.Click+=(_,_)=>_operation?.Cancel();
        download.Click+=async(_,_)=>{
            var target=Selected();var release=Release(target);if(target is null||release is null||UpdateService.PreparationBlocked(target,release) is not null)return;
            _operation=CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);_busy=true;check.Enabled=false;cancel.Enabled=true;cancel.Visible=true;progress.Value=0;progress.Visible=true;Buttons();state.Text="正在下载并校验 "+target.Name+" 的更新…";
            try {
                var prepared=await service.DownloadAsync(release,Application.ProductVersion.Split('+')[0],new Progress<int>(value=>{if(!IsDisposed)progress.Value=Math.Clamp(value,0,100);}),_operation.Token);
                _operation.Token.ThrowIfCancellationRequested();
                var current=capture().SingleOrDefault(d=>d.Id==target.Id);
                if(!UpdateService.SameTarget(target,current,release))throw new InvalidOperationException("设备或版本已改变，请重新检查更新。");
                cancel.Enabled=false;state.Text=target.Component=="tab5"?"校验通过，正在准备设备升级；仍需在 TAB5 上确认安装。":"校验通过，正在打开安装工具…";
                await install(current,prepared);
                if(!IsDisposed)state.Text=target.Component=="tab5"?"已交给 TAB5 升级工具。请在设备上确认安装，并在升级工具中核验启动。":"已交给安装工具。完成后重新检查运行版本。";
            }catch(OperationCanceledException){if(!IsDisposed)state.Text="操作已取消，未安装或刷写。";}
            catch(Exception ex){if(!IsDisposed)state.Text="更新未继续："+ex.Message;}
            finally{_operation?.Dispose();_operation=null;_busy=false;if(!IsDisposed){cancel.Enabled=false;cancel.Visible=false;check.Enabled=true;progress.Visible=false;RefreshRows();}}
        };
        site.LinkClicked+=(_,_)=>{try{Process.Start(new ProcessStartInfo("https://github.com/yaoyouzhong/AI-bot/blob/main/DOWNLOADS.md"){UseShellExecute=true});}catch(Exception ex){state.Text=ex.Message;}};
        Shown+=(_,_)=>{CatalogChanged();_timer.Start();if(service.CheckedAt is null)check.PerformClick();};_timer.Tick+=(_,_)=>RefreshRows();
        FormClosing+=(_,_)=>{_operation?.Cancel();_stop.Cancel();};SettingsWindow.FitScreen(this);
        ResumeLayout(true);
    }
    protected override void Dispose(bool disposing){if(disposing&&!_disposed){_disposed=true;_timer.Dispose();_operation?.Cancel();_stop.Cancel();_stop.Dispose();}base.Dispose(disposing);}
}
