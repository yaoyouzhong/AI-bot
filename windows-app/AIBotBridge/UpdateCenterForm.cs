using System.Diagnostics;
namespace AIBotBridge;
internal sealed record UpdateDevice(string Id,string Name,string Version,string Status,bool Enabled,string Action,string Requirement)
{
    internal string Component=>Action=="bridge"?"bridge":Action=="upgrade-tab5"?"tab5":"esp8266";
}
internal sealed record UpdatePreferences(bool Automatic=true,string[]? Notified=null);
internal sealed class UpdateCenterForm : Form
{
    private readonly CancellationTokenSource _stop=new();
    private readonly System.Windows.Forms.Timer _timer=new(){Interval=2000};
    private CancellationTokenSource? _operation;
    private bool _disposed,_busy;
    internal UpdateCenterForm(Func<UpdateDevice[]> capture,Func<UpdateDevice,PreparedUpdate,Task> install,UpdateService service) {
        Text="AI-bot · 软件与固件更新";Font=new("Microsoft YaHei UI",9);AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new(760,420);MinimumSize=new(650,360);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(18),ColumnCount=1,RowCount=6};Controls.Add(root);
        root.ColumnStyles.Add(new(SizeType.Percent,100));for(int i=0;i<6;i++)root.RowStyles.Add(new(i==2?SizeType.Percent:SizeType.AutoSize,i==2?100:0));
        root.Controls.Add(new Label{Text="电脑软件与已添加设备分别检查更新，自动选择适用的安装包。",AutoSize=true,Dock=DockStyle.Fill},0,0);
        var tools=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};var check=new Button{Text="检查更新",AutoSize=true,Name="check"};
        var automatic=new CheckBox{Text="自动检查并提醒",AutoSize=true,Checked=UpdateReminder.Read().Automatic};
        tools.Controls.AddRange([check,automatic]);root.Controls.Add(tools,0,1);
        var grid=new DataGridView{Name="updates",Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect};
        foreach(string heading in new[]{"软件 / 设备","当前版本","可用版本","状态"})grid.Columns.Add(heading,heading);
        foreach(DataGridViewColumn column in grid.Columns)column.SortMode=DataGridViewColumnSortMode.NotSortable;
        grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;root.Controls.Add(grid,0,2);
        var state=new Label{Name="state",AutoSize=true,Dock=DockStyle.Fill,MaximumSize=new(700,0)};root.Controls.Add(state,0,3);
        var progress=new ProgressBar{Dock=DockStyle.Fill,Visible=false};root.Controls.Add(progress,0,4);
        var actions=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};var notes=new Button{Text="所选更新说明",AutoSize=true};
        var download=new Button{Name="download",Text="下载并升级",AutoSize=true};var cancel=new Button{Text="取消下载",AutoSize=true,Enabled=false};var site=new LinkLabel{Text="手动下载与安装说明",AutoSize=true,Margin=new(12,9,0,0)};
        actions.Controls.AddRange([notes,download,cancel,site]);root.Controls.Add(actions,0,5);
        UpdateDevice[] rows=[];bool refreshing=false;
        UpdateDevice? Selected()=>grid.CurrentCell is {} c&&c.RowIndex<rows.Length?rows[c.RowIndex]:null;
        ComponentUpdate? Release(UpdateDevice? d)=>d is not null&&service.Available.TryGetValue(d.Component,out var u)?u:null;
        void Buttons(){var d=Selected();download.Enabled=!_busy&&d is not null&&UpdateService.Blocked(d,Release(d)) is null;download.Text=d?.Component=="bridge"?"下载并安装电脑端":"下载并准备升级";notes.Enabled=Release(d) is not null;}
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
                for(int i=0;i<rows.Length;i++){
                    var d=rows[i];var u=Release(d);object[] values=[d.Name,d.Version,u?.Version??"待查询",UpdateService.Blocked(d,u)??"有更新 · "+d.Requirement];
                    for(int j=0;j<values.Length;j++)if(!Equals(grid[j,i].Value,values[j]))grid[j,i].Value=values[j];
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
        notes.Click+=(_,_)=>{if(Release(Selected()) is not {} u)return;using var detail=new Form{Text="更新说明 · "+u.Version,ClientSize=new(600,440),Font=Font,StartPosition=FormStartPosition.CenterParent};detail.Controls.Add(new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,Text=u.Notes});detail.ShowDialog(this);};
        cancel.Click+=(_,_)=>_operation?.Cancel();
        download.Click+=async(_,_)=>{
            var target=Selected();var release=Release(target);if(target is null||release is null||UpdateService.Blocked(target,release) is not null)return;
            _operation=CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);_busy=true;check.Enabled=false;cancel.Enabled=true;progress.Value=0;progress.Visible=true;Buttons();state.Text="正在下载并校验 "+target.Name+" 的更新…";
            try {
                var prepared=await service.DownloadAsync(release,Application.ProductVersion.Split('+')[0],new Progress<int>(value=>{if(!IsDisposed)progress.Value=Math.Clamp(value,0,100);}),_operation.Token);
                _operation.Token.ThrowIfCancellationRequested();
                var current=capture().SingleOrDefault(d=>d.Id==target.Id);
                if(current is null||current.Component!=target.Component||UpdateService.Blocked(current,release) is not null)throw new InvalidOperationException("设备或版本已改变，请重新检查更新。");
                cancel.Enabled=false;state.Text=target.Component=="tab5"?"校验通过，正在准备设备升级；仍需在 TAB5 上确认安装。":"校验通过，正在打开安装工具…";
                await install(current,prepared);
                if(!IsDisposed)state.Text=target.Component=="tab5"?"已交给 TAB5 升级工具。请在设备上确认安装，并在升级工具中核验启动。":"已交给安装工具。完成后重新检查运行版本。";
            }catch(OperationCanceledException){if(!IsDisposed)state.Text="下载已取消，未安装或刷写。";}
            catch(Exception ex){if(!IsDisposed)state.Text="更新未继续："+ex.Message;}
            finally{_operation?.Dispose();_operation=null;_busy=false;if(!IsDisposed){cancel.Enabled=false;check.Enabled=true;progress.Visible=false;RefreshRows();}}
        };
        site.LinkClicked+=(_,_)=>{try{Process.Start(new ProcessStartInfo("https://github.com/yaoyouzhong/AI-bot/blob/main/docs/INSTALL.zh.md"){UseShellExecute=true});}catch(Exception ex){state.Text=ex.Message;}};
        Shown+=(_,_)=>{CatalogChanged();_timer.Start();if(service.CheckedAt is null)check.PerformClick();};_timer.Tick+=(_,_)=>RefreshRows();
        FormClosing+=(_,_)=>{_operation?.Cancel();_stop.Cancel();};SettingsWindow.FitScreen(this);
    }
    protected override void Dispose(bool disposing){if(disposing&&!_disposed){_disposed=true;_timer.Dispose();_operation?.Cancel();_stop.Cancel();_stop.Dispose();}base.Dispose(disposing);}
}
