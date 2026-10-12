using System.Diagnostics;
namespace AIBotBridge;
internal sealed class GalleryPackForm : Form
{
    private readonly CancellationTokenSource _stop=new();
    private CancellationTokenSource? _operation;
    private readonly UpdateService _service;
    private readonly bool _ownsService;
    private bool _disposed,_busy;
    internal Exception? OperationError {get;private set;}
    internal GalleryPackForm(UpdateService? service=null,Action<string>? openUrl=null) {
        _service=service??new UpdateService();_ownsService=service is null;
        openUrl??=url=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
        Text="AI-bot · 屏保图库";Font=SettingsWindow.BodyFont();AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        BackColor=Color.White;ClientSize=new(680,430);MinimumSize=new(560,380);AutoScroll=true;
        var root=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new(24),ColumnCount=1,RowCount=8};Controls.Add(root);
        root.ColumnStyles.Add(new(SizeType.Percent,100));for(int i=0;i<8;i++)root.RowStyles.Add(new(SizeType.AutoSize));
        var description=new Label{Text="图片和作品导读随图库一起更新，支持断点续传。",AutoSize=true,Dock=DockStyle.Fill,Margin=new(0,0,0,16)};root.Controls.Add(description,0,0);
        var currentLabels=new Dictionary<string,Label>();var latestLabels=new Dictionary<string,Label>();var statusLabels=new Dictionary<string,Label>();var updateButtons=new Dictionary<string,Button>();
        foreach(string category in new[]{"painting","calligraphy"}) {
            var card=new TableLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,ColumnCount=2,RowCount=4,Padding=new(14),Margin=new(0,0,0,10),BackColor=Color.FromArgb(246,248,251)};
            card.ColumnStyles.Add(new(SizeType.Percent,100));card.ColumnStyles.Add(new(SizeType.AutoSize));
            for(int i=0;i<4;i++)card.RowStyles.Add(new(SizeType.AutoSize));
            card.Controls.Add(new Label{Text=GalleryPack.CategoryName(category),AutoSize=true,Font=new(Font,FontStyle.Bold),Margin=new(0,0,0,5)},0,0);
            var status=new Label{Name=category+"-status",AutoSize=true,Anchor=AnchorStyles.Right,Margin=new(8,0,0,5)};card.Controls.Add(status,1,0);
            var current=new Label{Name=category+"-installed",AutoSize=true,Dock=DockStyle.Fill,Margin=new(0,0,0,4)};
            var latest=new Label{Name=category+"-latest",AutoSize=true,Dock=DockStyle.Fill,Margin=Padding.Empty};
            var update=new Button{Name=category+"-update",Text="下载并安装",Enabled=false,Anchor=AnchorStyles.Right,Margin=new(0,10,0,0)};SettingsWindow.StyleButton(update,true);
            card.Controls.Add(current,0,1);card.SetColumnSpan(current,2);card.Controls.Add(latest,0,2);card.SetColumnSpan(latest,2);card.Controls.Add(update,1,3);
            void FitCard(){int width=Math.Max(100,card.ClientSize.Width-card.Padding.Horizontal);current.MaximumSize=new(width,0);latest.MaximumSize=new(width,0);}
            card.SizeChanged+=(_,_)=>FitCard();update.VisibleChanged+=(_,_)=>FitCard();
            currentLabels[category]=current;latestLabels[category]=latest;statusLabels[category]=status;updateButtons[category]=update;
            root.Controls.Add(card,0,category=="painting"?1:2);
        }
        var warning=new Label{Text="旧图库保留为备份。",AutoSize=true,Dock=DockStyle.Fill,Margin=new(0,2,0,14)};root.Controls.Add(warning,0,3);
        var tools=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill,Margin=Padding.Empty};
        var check=new Button{Name="gallery-check",Text="检查更新",Margin=new(0,0,8,0)};SettingsWindow.StyleButton(check);
        var import=new Button{Name="gallery-import",Text="导入图库 ZIP…",Margin=new(0,0,8,0)};SettingsWindow.StyleButton(import);
        var browse=new Button{Name="gallery-browse",Text="浏览作品",Margin=new(0,0,8,0)};SettingsWindow.StyleButton(browse);
        var open=new Button{Text="打开图库目录",Margin=new(0,0,8,0)};SettingsWindow.StyleButton(open);
        var cancel=new Button{Name="gallery-cancel",Text="取消",Visible=false};SettingsWindow.StyleButton(cancel);
        tools.Controls.AddRange([browse,check,import,open,cancel]);root.Controls.Add(tools,0,4);
        var message=new Label{Name="gallery-state",AutoSize=true,Dock=DockStyle.Fill,Margin=new(0,12,0,10)};root.Controls.Add(message,0,5);
        var progress=new ProgressBar{Name="gallery-progress",Dock=DockStyle.Fill,Height=8,Visible=false,Margin=new(0,0,0,12)};root.Controls.Add(progress,0,6);
        var site=new LinkLabel{Name="gallery-download-center",Text="完整下载与安装说明",AutoSize=true,Margin=Padding.Empty};root.Controls.Add(site,0,7);
        bool fitting=false,fitPending=false;
        void FitContentHeight() {
            if(fitting||IsDisposed||!Visible)return;fitting=true;
            try{Height=Math.Min(root.Height+Height-ClientSize.Height,Screen.FromControl(this).WorkingArea.Height-32);if(ClientSize.Height>=root.Height)AutoScrollPosition=Point.Empty;}
            finally{fitting=false;}
        }
        root.Layout+=(_,_)=>{if(fitPending||fitting||!IsHandleCreated||IsDisposed)return;fitPending=true;BeginInvoke((Action)(()=>{fitPending=false;FitContentHeight();}));};
        root.SizeChanged+=(_,_)=>{int width=Math.Max(100,root.ClientSize.Width-root.Padding.Horizontal);foreach(var label in new[]{description,warning,message})label.MaximumSize=new(width,0);};
        void RefreshStatus() {
            check.Enabled=import.Enabled=browse.Enabled=!_busy;
            foreach(string category in currentLabels.Keys) {
                var installed=GalleryPack.Installed(category);_service.Galleries.TryGetValue(category,out var release);
                currentLabels[category].Text=installed is null?"已安装：尚未导入图库包":$"已安装：{installed.Version} · {installed.Works} 件";
                string? blocked=GalleryUpdateCatalog.Blocked(installed,release);
                latestLabels[category].Text=release is null?(_service.CheckedAt is null?"最新精选：尚未检查":"最新精选：暂无正式发布"):
                    $"最新精选：{release.Version}"+(release.Package is {} pack?$" · {pack.Size/1048576d:0.#} MiB":"");
                string status=blocked??(installed is null?"可安装":"有更新");
                statusLabels[category].Text=status switch{"已是最新版本"=>"已是最新","本机版本较新"=>"本机较新","暂无可确认的正式精选集"=>"待检查","发布包缺少有效下载或校验信息"=>"暂不可用",_=>status};
                statusLabels[category].ForeColor=blocked is null?Color.FromArgb(34,109,215):Color.FromArgb(88,101,119);
                latestLabels[category].ForeColor=Color.FromArgb(88,101,119);
                updateButtons[category].Text=installed is null?"下载并安装":"下载并升级";
                updateButtons[category].Visible=blocked is not ("已是最新版本" or "本机版本较新");
                updateButtons[category].Enabled=!_busy&&blocked is null;
            }
        }
        void StartOperation(bool transfer) {
            OperationError=null;
            _busy=true;_operation=CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);cancel.Visible=true;cancel.Enabled=true;
            progress.Visible=transfer;progress.Value=0;RefreshStatus();
        }
        void EndOperation() {
            _operation?.Dispose();_operation=null;_busy=false;
            if(!IsDisposed){cancel.Visible=false;progress.Visible=false;RefreshStatus();}
        }
        async Task CheckAsync() {
            if(_busy)return;StartOperation(false);message.Text="正在检查名画和书法的正式精选集…";
            try {
                await _service.CheckAsync(_operation!.Token);
                if(!IsDisposed)message.Text="已检查线上精选集 · "+_service.CheckedAt!.Value.ToLocalTime().ToString("MM-dd HH:mm");
            }catch(Exception ex){OperationError=ex;if(!IsDisposed)message.Text="检查未完成，保留上次结果："+ex.Message;}
            finally{EndOperation();}
        }
        check.Click+=async(_,_)=>await CheckAsync();
        foreach(var (category,button) in updateButtons)button.Click+=async(_,_)=>{
            if(_busy||!_service.Galleries.TryGetValue(category,out var release)||GalleryUpdateCatalog.Blocked(GalleryPack.Installed(category),release) is not null)return;
            StartOperation(true);var token=_operation!.Token;bool importing=false;int shown=-1;
            message.Text="正在下载并校验"+GalleryPack.CategoryName(category)+"精选集 "+release.Version+"…";
            PreparedGalleryUpdate? prepared=null;bool installed=false;
            try {
                prepared=await _service.DownloadGalleryAsync(release,new Progress<int>(v=>{if(!IsDisposed&&!importing&&v!=shown){shown=v;progress.Value=Math.Clamp(v*70/100,0,70);message.Text=$"正在下载并校验{GalleryPack.CategoryName(category)}精选集 {release.Version} · {v}%（支持断点续传）";}}),token);
                token.ThrowIfCancellationRequested();importing=true;progress.Value=70;
                if(GalleryUpdateCatalog.Blocked(GalleryPack.Installed(category),release) is {} reason)throw new InvalidOperationException(reason);
                message.Text="下载校验通过，正在验证和安装；旧图库保留到新包校验完成。";
                var updates=new Progress<int>(v=>{if(!IsDisposed)progress.Value=Math.Clamp(70+v*30/100,70,100);});
                var result=await Task.Run(()=>{
                    if(!FirmwareFlasher.MatchesHash(prepared.File,prepared.Sha256))throw new InvalidDataException("下载文件已改变，原图库保持不变。");
                    return GalleryPack.Install(prepared.File,progress:updates,cancellation:token,expected:release);
                },token);
                installed=true;
                if(!IsDisposed)message.Text=$"{GalleryPack.CategoryName(category)}已更新 · {result.Version} · {result.Works} 件\n旧版保留为备份。"+(result.SchemaVersion==2?"图片和导读已一起更新。":"");
            }catch(OperationCanceledException){if(!IsDisposed)message.Text="更新已取消，原图库保持不变；已下载进度保留，下次继续。";}
            catch(Exception ex){OperationError=ex;if(!IsDisposed)message.Text="更新未完成："+ex.Message+" 可重试继续下载，原图库保持不变。";}
            finally {
                if(prepared is not null&&installed) {
                    try{await Task.Run(()=>UpdateService.ClearGalleryDownload(prepared));}
                    catch(Exception ex){if(!IsDisposed)message.Text+=" 下载缓存未清理："+ex.Message;}
                }
                EndOperation();
            }
        };
        cancel.Click+=(_,_)=>{cancel.Enabled=false;_operation?.Cancel();};
        browse.Click+=(_,_)=>{using var form=new GalleryBrowserForm();form.ShowDialog(this);};
        site.LinkClicked+=(_,_)=>{try{openUrl("https://github.com/yaoyouzhong/AI-bot/blob/main/DOWNLOADS.md");}catch(Exception ex){message.Text=ex.Message;}};
        open.Click+=(_,_)=>{try{Directory.CreateDirectory(GalleryPack.Root);Process.Start(new ProcessStartInfo(GalleryPack.Root){UseShellExecute=true});}catch(Exception ex){message.Text=ex.Message;}};
        import.Click+=async(_,_)=>{
            if(_busy)return;
            using var select=new OpenFileDialog{Title="选择名画或书法图库包",Filter="AI-bot 图库包 (*.zip)|*.zip",CheckFileExists=true};if(select.ShowDialog(this)!=DialogResult.OK)return;
            StartOperation(true);message.Text="正在校验和导入；现有图库保留到新包校验完成。";
            try {
                var updates=new Progress<int>(v=>{if(!IsDisposed)progress.Value=v;});
                var result=await Task.Run(()=>GalleryPack.Install(select.FileName,progress:updates,cancellation:_operation!.Token),_operation!.Token);
                if(!IsDisposed)message.Text=$"{GalleryPack.CategoryName(result.Category)}导入完成 · {result.Version} · {result.Works} 件。旧版已保留为备份。"+(result.SchemaVersion==2?"图片和导读已一起更新。":"");
            }catch(OperationCanceledException){if(!IsDisposed)message.Text="导入已取消，原图库保持不变。";}
            catch(Exception ex){OperationError=ex;if(!IsDisposed)message.Text="未完成导入："+ex.Message;}
            finally{EndOperation();}
        };
        SettingsWindow.AddTitle(root,"屏保图库");Shown+=async(_,_)=>{FitContentHeight();await CheckAsync();};FormClosing+=(_,_)=>_stop.Cancel();RefreshStatus();SettingsWindow.FitScreen(this);
    }
    protected override void Dispose(bool disposing){if(disposing&&!_disposed){_disposed=true;_stop.Cancel();_stop.Dispose();if(_ownsService)_service.Dispose();}base.Dispose(disposing);}
}
