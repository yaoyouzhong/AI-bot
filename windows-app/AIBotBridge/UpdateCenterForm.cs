using System.Diagnostics;
using System.Text.Json;

namespace AIBotBridge;
internal sealed record UpdateDevice(string Id,string Name,string Version,string Status,bool Enabled,string Action,string Requirement);
internal sealed class UpdateCenterForm : Form
{
    private readonly CancellationTokenSource _stop=new();
    private bool _disposed;
    private readonly System.Windows.Forms.Timer _timer=new(){Interval=2000};
    internal UpdateCenterForm(Func<UpdateDevice[]> capture,Action<string,string> open) {
        Text="AI-bot · 软件与固件";Font=new("Microsoft YaHei UI",9);AutoScaleDimensions=new(96,96);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new(650,350);MinimumSize=new(560,320);
        var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(18),ColumnCount=1,RowCount=6};Controls.Add(root);
        root.ColumnStyles.Add(new(SizeType.Percent,100));for(int i=0;i<6;i++)root.RowStyles.Add(new(i==3?SizeType.Percent:SizeType.AutoSize,i==3?100:0));
        var version=new Label{Text="电脑桥接 · 当前版本 "+Application.ProductVersion.Split('+')[0],AutoSize=true};root.Controls.Add(version,0,0);
        var buttons=new FlowLayoutPanel{AutoSize=true,Dock=DockStyle.Fill};var check=new Button{Text="检查更新",AutoSize=true};var releases=new Button{Text="下载页面",AutoSize=true};buttons.Controls.AddRange([check,releases]);root.Controls.Add(buttons,0,1);
        var state=new Label{Text="",AutoSize=true,Dock=DockStyle.Fill};root.Controls.Add(state,0,2);
        var grid=new DataGridView{Dock=DockStyle.Fill,Height=150,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,RowHeadersVisible=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells,BackgroundColor=Color.White,BorderStyle=BorderStyle.None,MultiSelect=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect};
        foreach(string heading in new[]{"设备","运行版本","升级方式"})grid.Columns.Add(heading,heading);grid.DefaultCellStyle.WrapMode=DataGridViewTriState.True;root.Controls.Add(grid,0,3);
        string releaseNotes="";var notes=new LinkLabel{Text="更新说明",AutoSize=true,Visible=false};root.Controls.Add(notes,0,4);
        notes.LinkClicked+=(_,_)=>{using var detail=new Form{Text="更新说明",ClientSize=new(560,420),Font=Font,StartPosition=FormStartPosition.CenterParent};detail.Controls.Add(new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,Text=releaseNotes});detail.ShowDialog(this);};
        var action=new Button{Text="升级所选设备",AutoSize=true,Anchor=AnchorStyles.Right};root.Controls.Add(action,0,5);
        UpdateDevice[] rows=[];
        void RefreshDevices(){string? selected=grid.CurrentCell is {} cell&&cell.RowIndex<rows.Length?rows[cell.RowIndex].Id:null;rows=capture();grid.Rows.Clear();foreach(var d in rows)grid.Rows.Add(d.Name,d.Version,d.Requirement);int index=Array.FindIndex(rows,d=>d.Id==selected);if(index>=0)grid.CurrentCell=grid[0,index];action.Enabled=grid.CurrentCell is {} c&&c.RowIndex<rows.Length&&rows[c.RowIndex].Enabled;}
        grid.SelectionChanged+=(_,_)=>action.Enabled=grid.CurrentCell is {} c&&c.RowIndex<rows.Length&&rows[c.RowIndex].Enabled;
        action.Click+=(_,_)=>{if(grid.CurrentCell is {} c&&c.RowIndex<rows.Length){var d=rows[c.RowIndex];open(d.Id,d.Action);}};
        check.Click+=async(_,_)=>{
            check.Enabled=false;state.Text="正在查询项目正式发布…";
            try {
                using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(15),MaxResponseContentBufferSize=262144};http.DefaultRequestHeaders.UserAgent.ParseAdd("AI-bot/0.4");
                string text=await http.GetStringAsync("https://api.github.com/repos/yaoyouzhong/AI-bot/releases/latest",_stop.Token);
                using var doc=JsonDocument.Parse(text);var release=doc.RootElement;
                if(release.GetProperty("draft").GetBoolean()||release.GetProperty("prerelease").GetBoolean())throw new InvalidDataException("返回的不是正式发布。");
                string tag=release.GetProperty("tag_name").GetString()??"";string body=release.GetProperty("body").GetString()??"暂无更新说明。";
                if(!Version.TryParse(tag.TrimStart('v'),out var latest)||!Version.TryParse(Application.ProductVersion.Split('+')[0],out var installed))throw new InvalidDataException("版本格式无法比较。");
                if(!IsDisposed){state.Text=latest>installed?$"可更新：{installed} → {latest}":$"最新正式版 {latest} · 当前 {installed}";releaseNotes=body;notes.Visible=true;}
            }catch(OperationCanceledException){if(!IsDisposed)state.Text="检查已取消或超时，可稍后重试。";}
            catch(Exception ex) when(ex is HttpRequestException or JsonException or InvalidDataException or KeyNotFoundException){if(!IsDisposed)state.Text="未能确认新版本："+ex.Message;}
            finally{if(!IsDisposed)check.Enabled=true;}
        };
        releases.Click+=(_,_)=>{try{Process.Start(new ProcessStartInfo("https://github.com/yaoyouzhong/AI-bot/releases"){UseShellExecute=true});}catch(Exception ex){state.Text=ex.Message;}};
        Shown+=(_,_)=>{RefreshDevices();_timer.Start();};_timer.Tick+=(_,_)=>RefreshDevices();FormClosing+=(_,_)=>_stop.Cancel();SettingsWindow.FitScreen(this);
    }
    protected override void Dispose(bool disposing){if(disposing&&!_disposed){_disposed=true;_timer.Stop();_timer.Dispose();_stop.Cancel();_stop.Dispose();}base.Dispose(disposing);}
}
