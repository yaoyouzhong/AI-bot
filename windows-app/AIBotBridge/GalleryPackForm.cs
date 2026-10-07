using System.Diagnostics;
namespace AIBotBridge;
internal sealed class GalleryPackForm : Form
{
    private readonly CancellationTokenSource _stop=new();
    private bool _disposed;
    internal GalleryPackForm() {
        Text="AI-bot · 屏保图库";Font=new("Microsoft YaHei UI",10);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new(620,300);MinimumSize=new(560,300);
        var root=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new(20),AutoScroll=true};Controls.Add(root);
        root.Controls.Add(new Label{Text="名画和书法可分别下载，导入后无需重启。程序升级会保留图库。",AutoSize=true,MaximumSize=new(550,0)});
        var status=new Label{AutoSize=true,Margin=new(0,16,0,14),MaximumSize=new(550,0)};root.Controls.Add(status);
        void RefreshStatus(){status.Text=string.Join("\n",new[]{"painting","calligraphy"}.Select(c=>GalleryPack.Installed(c) is {} p?$"{GalleryPack.CategoryName(c)}：{p.Works} 件 · {p.Version}":$"{GalleryPack.CategoryName(c)}：未导入下载包（原有程序目录图库仍可使用）"));}
        var buttons=new FlowLayoutPanel{AutoSize=true};var download=new Button{Text="下载图库",AutoSize=true};var import=new Button{Text="导入图库 ZIP…",AutoSize=true};var open=new Button{Text="打开图库目录",AutoSize=true};buttons.Controls.AddRange([download,import,open]);root.Controls.Add(buttons);
        var progress=new ProgressBar{Width=550,Visible=false};root.Controls.Add(progress);
        var message=new Label{AutoSize=true,MaximumSize=new(550,0),Margin=new(0,12,0,0),Text="在下载页选择名画包或书法包，然后导入 ZIP。无需手动解压到程序目录。"};root.Controls.Add(message);
        download.Click+=(_,_)=>{try{Process.Start(new ProcessStartInfo("https://github.com/yaoyouzhong/AI-bot/releases/tag/tab5-v0.2.145-ui"){UseShellExecute=true});}catch(Exception ex){message.Text=ex.Message;}};
        open.Click+=(_,_)=>{try{Directory.CreateDirectory(GalleryPack.Root);Process.Start(new ProcessStartInfo(GalleryPack.Root){UseShellExecute=true});}catch(Exception ex){message.Text=ex.Message;}};
        import.Click+=async(_,_)=>{
            using var select=new OpenFileDialog{Title="选择名画或书法图库包",Filter="AI-bot 图库包 (*.zip)|*.zip",CheckFileExists=true};if(select.ShowDialog(this)!=DialogResult.OK)return;
            import.Enabled=false;progress.Visible=true;progress.Value=0;message.Text="正在校验和导入；现有图库保留到新包校验完成。";
            try {
                var updates=new Progress<int>(v=>{if(!IsDisposed)progress.Value=v;});
                var result=await Task.Run(()=>GalleryPack.Install(select.FileName,progress:updates,cancellation:_stop.Token),_stop.Token);
                if(!IsDisposed){RefreshStatus();message.Text=$"{GalleryPack.CategoryName(result.Category)}导入完成。请在 TAB5 设置中选择对应屏保。";}
            }catch(OperationCanceledException){if(!IsDisposed)message.Text="导入已取消，原图库保持不变。";}
            catch(Exception ex){if(!IsDisposed)message.Text="未完成导入："+ex.Message;}
            finally{if(!IsDisposed){import.Enabled=true;progress.Visible=false;}}
        };
        FormClosing+=(_,_)=>_stop.Cancel();RefreshStatus();SettingsWindow.FitScreen(this);
    }
    protected override void Dispose(bool disposing){if(disposing&&!_disposed){_disposed=true;_stop.Cancel();_stop.Dispose();}base.Dispose(disposing);}
}
