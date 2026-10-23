using System.Diagnostics;
namespace AIBotBridge;
internal sealed class GalleryPackForm : Form
{
    private readonly CancellationTokenSource _stop=new();
    private CancellationTokenSource? _operation;
    private readonly UpdateService _service;
    private readonly bool _ownsService;
    private readonly ContextMenuStrip _moreMenu=new();
    private bool _disposed,_busy;
    internal Exception? OperationError {get;private set;}
    internal GalleryPackForm(UpdateService? service=null,Action<string>? openUrl=null,Func<string?>? selectPackage=null) {
        _service=service??new UpdateService();_ownsService=service is null;
        openUrl??=url=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
        Text="AI-bot · 屏保图库";Font=SettingsWindow.BodyFont();AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        BackColor=Color.White;ClientSize=new(760,430);MinimumSize=new(740,380);AutoScroll=true;
        var root=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Padding=new(24),ColumnCount=1,RowCount=7};Controls.Add(root);
        root.ColumnStyles.Add(new(SizeType.Percent,100));for(int i=0;i<7;i++)root.RowStyles.Add(new(SizeType.AutoSize));
        var description=new Label{Text="选择喜欢的作品集，在电脑欣赏，也可用于 TAB5 屏保。",AutoSize=true,Dock=DockStyle.Fill,Margin=new(0,0,0,16)};root.Controls.Add(description,0,0);
        var hdLabels=new Dictionary<string,Label>();
        var currentLabels=new Dictionary<string,Label>();var latestLabels=new Dictionary<string,Label>();var statusLabels=new Dictionary<string,Label>();var updateButtons=new Dictionary<string,Button>();
        foreach(string category in new[]{"painting","calligraphy"}) {
            var card=new TableLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Padding=new(14),Margin=new(0,0,0,10),BackColor=Color.FromArgb(246,248,251)};
            card.ColumnStyles.Add(new(SizeType.Percent,100));card.ColumnStyles.Add(new(SizeType.AutoSize));
            for(int i=0;i<3;i++)card.RowStyles.Add(new(SizeType.AutoSize));
            card.Controls.Add(new Label{Text=GalleryPack.CategoryName(category),AutoSize=true,Font=new(Font,FontStyle.Bold),Margin=new(0,0,0,5)},0,0);
            var status=new Label{Name=category+"-status",AutoSize=true,Anchor=AnchorStyles.Right,Margin=new(8,0,0,5)};card.Controls.Add(status,1,0);
            var current=new Label{Name=category+"-installed",AutoSize=true,Dock=DockStyle.Fill,Margin=new(0,0,0,4)};
            var latest=new Label{Name=category+"-latest",AutoSize=true,Dock=DockStyle.Fill,Margin=Padding.Empty};
            var update=new Button{Name=category+"-update",Text="下载并安装",Enabled=false,Anchor=AnchorStyles.Right,Margin=new(0,10,0,0)};SettingsWindow.StyleButton(update,true);
            card.Controls.Add(current,0,1);card.Controls.Add(latest,0,2);card.Controls.Add(update,1,2);
            var hd=new Label{Name=category+"-hd",AutoSize=true,Dock=DockStyle.Fill,ForeColor=SettingsWindow.Muted,Anchor=AnchorStyles.Right,Margin=new(8,0,0,4)};card.Controls.Add(hd,1,1);hdLabels[category]=hd;
            void FitCard(){int width=Math.Max(100,card.ClientSize.Width-card.Padding.Horizontal);current.MaximumSize=new(Math.Max(100,width-Math.Max(update.Width,hd.Width)-24),0);latest.MaximumSize=current.MaximumSize;}
            card.SizeChanged+=(_,_)=>FitCard();update.VisibleChanged+=(_,_)=>FitCard();
            currentLabels[category]=current;latestLabels[category]=latest;statusLabels[category]=status;updateButtons[category]=update;
            root.Controls.Add(card,0,category=="painting"?1:2);
        }
        var editions=new TableLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,ColumnCount=2,RowCount=3,Margin=new(0,12,0,20)};
        editions.ColumnStyles.Add(new(SizeType.AutoSize));editions.ColumnStyles.Add(new(SizeType.Percent,100));
        for(int i=0;i<3;i++)editions.RowStyles.Add(new(SizeType.AutoSize));
        var editionDescriptions=new List<Label>();
        foreach(var (name,detail,row) in new[]{("标准版","电脑使用适配后的展示图",0),("高清版","电脑使用高清图片，可查看更多细节",1)}){
            editions.Controls.Add(new Label{Text=name,AutoSize=true,Font=new(Font,FontStyle.Bold),ForeColor=SettingsWindow.Ink,Margin=new(0,0,24,10)},0,row);
            var detailLabel=new Label{Text=detail,AutoSize=true,Dock=DockStyle.Fill,ForeColor=SettingsWindow.Ink,Margin=new(0,0,0,10)};
            editions.Controls.Add(detailLabel,1,row);editionDescriptions.Add(detailLabel);
        }
        var warning=new Label{Text="两版均含作品和导读，TAB5 显示相同，任选一版即可。",ForeColor=SettingsWindow.Muted,AutoSize=true,Dock=DockStyle.Fill,Margin=new(0,4,0,0)};
        editions.Controls.Add(warning,0,2);editions.SetColumnSpan(warning,2);root.Controls.Add(editions,0,3);
        editions.SizeChanged+=(_,_)=>{int width=Math.Max(100,editions.ClientSize.Width);warning.MaximumSize=new(width,0);int labelWidth=editions.Controls[0].GetPreferredSize(Size.Empty).Width+24;foreach(var label in editionDescriptions)label.MaximumSize=new(Math.Max(100,width-labelWidth),0);};
        var tools=new FlowLayoutPanel{Name="gallery-actions",AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,WrapContents=true,Margin=Padding.Empty};
        var check=new Button{Name="gallery-check",Text="检查更新",Margin=new(0,0,8,0)};SettingsWindow.StyleButton(check);
        var import=new Button{Name="gallery-import",Text="安装图库包…",Margin=new(0,0,8,0)};SettingsWindow.StyleButton(import);
        var browse=new Button{Name="gallery-browse",Text="浏览作品",Margin=new(0,0,8,0)};SettingsWindow.StyleButton(browse,true);
        var more=new Button{Name="gallery-more",Text="更多",Anchor=AnchorStyles.Top|AnchorStyles.Right,Margin=Padding.Empty};SettingsWindow.StyleButton(more);
        var open=new ToolStripMenuItem("打开图库目录"){Name="gallery-open-directory"};
        var site=new ToolStripMenuItem("下载与安装说明"){Name="gallery-download-center"};
        _moreMenu.Items.AddRange([open,site]);more.Click+=(_,_)=>_moreMenu.Show(more,new Point(0,more.Height));
        tools.Controls.AddRange([browse,import,check,more]);root.Controls.Add(tools,0,4);
        var operationRow=new TableLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Fill,Margin=new(0,12,0,0),ColumnCount=2,RowCount=1};
        operationRow.ColumnStyles.Add(new(SizeType.Percent,100));operationRow.ColumnStyles.Add(new(SizeType.AutoSize));operationRow.RowStyles.Add(new(SizeType.AutoSize));root.Controls.Add(operationRow,0,5);
        var message=new Label{Name="gallery-state",AutoSize=true,Dock=DockStyle.Fill,ForeColor=SettingsWindow.Muted,Margin=new(0,5,12,10)};operationRow.Controls.Add(message,0,0);
        var cancel=new Button{Name="gallery-cancel",Text="取消操作",Visible=false,Anchor=AnchorStyles.Top|AnchorStyles.Right,Margin=Padding.Empty};SettingsWindow.StyleButton(cancel);operationRow.Controls.Add(cancel,1,0);
        var progress=new ProgressBar{Name="gallery-progress",Dock=DockStyle.Fill,Height=8,Visible=false,Margin=new(0,0,0,12)};root.Controls.Add(progress,0,6);
        bool fitting=false,fitPending=false;
        void FitContentHeight() {
            if(fitting||IsDisposed||!Visible)return;fitting=true;
            try{Height=Math.Min(root.Height+Height-ClientSize.Height,Screen.FromControl(this).WorkingArea.Height-32);if(ClientSize.Height>=root.Height)AutoScrollPosition=Point.Empty;}
            finally{fitting=false;}
        }
        root.Layout+=(_,_)=>{if(fitPending||fitting||!IsHandleCreated||IsDisposed)return;fitPending=true;BeginInvoke((Action)(()=>{fitPending=false;FitContentHeight();}));};
        root.SizeChanged+=(_,_)=>{int width=Math.Max(100,root.ClientSize.Width-root.Padding.Horizontal);description.MaximumSize=new(width,0);};
        operationRow.SizeChanged+=(_,_)=>message.MaximumSize=new(Math.Max(100,operationRow.ClientSize.Width-(cancel.Visible?cancel.Width:0)-message.Margin.Horizontal),0);
        void RefreshStatus() {
            check.Enabled=import.Enabled=browse.Enabled=!_busy;
            foreach(string category in currentLabels.Keys) {
                var hd=GalleryHdPack.Installed(category);
                hdLabels[category].Text=hd is null?"标准画质":"高清画质";
                var installed=GalleryPack.Installed(category);if(installed is null)hdLabels[category].Text="";_service.Galleries.TryGetValue(category,out var release);
                currentLabels[category].Text=installed is null?"尚未安装":hd is not null?$"高清版 {hd.Edition}":$"标准版 {installed.Version}";
                string? blocked=GalleryUpdateCatalog.Blocked(installed,release);
                latestLabels[category].Text=release is null?(_service.CheckedAt is null?"线上标准版：尚未检查":"线上标准版：暂无正式发布"):
                    $"线上标准版：{release.Version}"+(release.Package is {} pack?$" · {pack.Size/1048576d:0.#} MiB":"");
                string status=blocked??(installed is null?"可安装":"有更新");
                statusLabels[category].Text=status switch{"已是最新版本"=>"已是最新","本机版本较新"=>"本机较新","暂无可确认的正式精选集"=>"待检查","发布包缺少有效下载或校验信息"=>"暂不可用",_=>status};
                if(hd is not null)statusLabels[category].Text="已安装高清版";
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
        site.Click+=(_,_)=>{try{openUrl("https://github.com/yaoyouzhong/AI-bot/blob/main/DOWNLOADS.md");}catch(Exception ex){message.Text=ex.Message;}};
        open.Click+=(_,_)=>{try{Directory.CreateDirectory(GalleryPack.Root);Process.Start(new ProcessStartInfo(GalleryPack.Root){UseShellExecute=true});}catch(Exception ex){message.Text=ex.Message;}};
        import.Click+=async(_,_)=>{
            if(_busy)return;
            using var select=new OpenFileDialog{Title="安装标准版或高清完整图库",Filter="AI-bot 图库包 (*.zip)|*.zip",CheckFileExists=true};
            string? selected=selectPackage is null?(select.ShowDialog(this)==DialogResult.OK?select.FileName:null):selectPackage();if(selected is null)return;
            StartOperation(true);message.Text="正在检查图库包，完成后自动启用…";
            try{
                int shown=-1;var updates=new Progress<int>(v=>{if(!IsDisposed&&v!=shown){shown=v;progress.Value=v;message.Text=$"正在校验并安装图库 · {v}%";}});
                string summary=await Task.Run(()=>{
                    bool standard;using(var zip=System.IO.Compression.ZipFile.OpenRead(selected))standard=zip.GetEntry("PACK.json") is not null;
                    if(standard){var result=GalleryPack.Install(selected,progress:updates,cancellation:_operation!.Token);return $"{GalleryPack.CategoryName(result.Category)}标准版已安装 · {result.Works} 件。";}
                    var hd=GalleryHdPack.Install(selected,progress:updates,cancellation:_operation!.Token);return $"最近安装：{GalleryPack.CategoryName(hd.Category)}高清版 {hd.Edition}，已启用。";
                },_operation!.Token);
                if(!IsDisposed)message.Text=summary;
            }catch(OperationCanceledException){if(!IsDisposed)message.Text="安装已取消，原有图库保持不变。";}
            catch(Exception ex){OperationError=ex;if(!IsDisposed)message.Text="图库安装失败："+ex.Message+" 原有图库保持不变。";}
            finally{EndOperation();}
        };
        tools.Height=operationRow.Height=1;
        SettingsWindow.AddTitle(root,"屏保图库");Shown+=async(_,_)=>{FitContentHeight();await CheckAsync();};FormClosing+=(_,_)=>_stop.Cancel();RefreshStatus();SettingsWindow.FitScreen(this);
    }
    protected override void Dispose(bool disposing){if(disposing&&!_disposed){_disposed=true;_stop.Cancel();_stop.Dispose();_moreMenu.Dispose();if(_ownsService)_service.Dispose();}base.Dispose(disposing);}
}
