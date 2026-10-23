using System.Security.Cryptography;
namespace AIBotBridge;

internal sealed class WifiUnlockForm : Form
{
    private readonly WifiKeyboardAccessStore store;
    private readonly ITightVncController component;
    private readonly Func<bool> canEdit;
    private readonly Func<CancellationToken,Task> check;
    private readonly CancellationTokenSource closed=new();
    private readonly CancellationToken lifetime;
    private readonly Tab5PinSwitch enabled;
    private readonly Button action,remove,test,save;
    private readonly Label componentState,feedback;
    private readonly TableLayoutPanel body;
    private readonly LinkLabel requirements;
    private TightVncState snapshot=new(TightVncKind.Conflict,Detail:"正在检查组件…");
    private string nextAction="";
    private bool busy;
    internal Func<bool> ConfirmRemoval {get;set;}
    internal WifiUnlockForm(WifiKeyboardAccessStore? store=null,Func<bool>? canEdit=null,Func<CancellationToken,Task>? check=null,ITightVncController? component=null) {
        lifetime=closed.Token;this.store=store??new();this.canEdit=canEdit??(()=>WindowsSessionLock.Read()==false);
        this.check=check??(async token=>{using var client=await WifiKeyboardRfb.ConnectAsync(token,initialize:false);});this.component=component??new TightVncController(this.store);
        ConfirmRemoval=()=>MessageBox.Show(this,"将卸载由 AI-bot 安装的 TightVNC Server，Wi-Fi 解锁将停用。Windows PIN 和配对保留。","卸载解锁组件",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)==DialogResult.OK;
        Text="TAB5 · Wi-Fi 一键解锁";UnlockSettingsStyle.Apply(this,320);MinimumSize=new(496,300);
        var outer=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new(28,24,28,24),ColumnCount=1,RowCount=2};
        outer.ColumnStyles.Add(new(SizeType.Percent,100));outer.RowStyles.Add(new(SizeType.Percent,100));outer.RowStyles.Add(new(SizeType.Absolute,60));Controls.Add(outer);
        var viewport=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Margin=Padding.Empty};outer.Controls.Add(viewport,0,0);
        body=new TableLayoutPanel{Dock=DockStyle.Top,ColumnCount=1,RowCount=7,Margin=Padding.Empty};body.ColumnStyles.Add(new(SizeType.Percent,100));viewport.Controls.Add(body);
        for(int i=0;i<7;i++)body.RowStyles.Add(new(SizeType.Absolute,0));
        void Add(Control control,int row){control.Dock=DockStyle.Fill;control.Margin=Padding.Empty;body.Controls.Add(control,0,row);}
        Add(new Label{Text="Wi-Fi 一键解锁",Font=SettingsWindow.TitleFont()},0);
        var toggleRow=new TableLayoutPanel{ColumnCount=2,RowCount=1,Margin=Padding.Empty};toggleRow.ColumnStyles.Add(new(SizeType.Percent,100));toggleRow.ColumnStyles.Add(new(SizeType.Absolute,52));Add(toggleRow,1);
        toggleRow.Controls.Add(new Label{Dock=DockStyle.Fill,Text="允许 Wi-Fi 解锁",TextAlign=ContentAlignment.MiddleLeft,Margin=Padding.Empty},0,0);
        enabled=new Tab5PinSwitch{Name="wifi-enabled",AccessibleName="允许 Wi-Fi 解锁",Checked=this.store.Enabled,Size=new(52,32),Anchor=AnchorStyles.Right,Margin=Padding.Empty};toggleRow.Controls.Add(enabled,1,0);
        Add(new Label{Text="TightVNC",Font=new Font(Font,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft},3);
        var componentRow=new TableLayoutPanel{ColumnCount=2,RowCount=1,Margin=Padding.Empty};componentRow.ColumnStyles.Add(new(SizeType.Percent,100));componentRow.ColumnStyles.Add(new(SizeType.AutoSize));Add(componentRow,4);
        componentState=new Label{Name="wifi-component-state",Text="正在检查…",TextAlign=ContentAlignment.MiddleLeft,Dock=DockStyle.Fill,Margin=Padding.Empty};componentRow.Controls.Add(componentState,0,0);
        var componentActions=new FlowLayoutPanel{WrapContents=false,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Anchor=AnchorStyles.Right,Margin=Padding.Empty,Padding=Padding.Empty};componentRow.Controls.Add(componentActions,1,0);
        action=UnlockSettingsStyle.Action("wifi-component-action","安装并启用",144);action.Anchor=AnchorStyles.Top|AnchorStyles.Left;action.Margin=new(0,0,12,0);componentActions.Controls.Add(action);
        remove=UnlockSettingsStyle.Action("wifi-component-remove","卸载组件",108);remove.Anchor=AnchorStyles.Top|AnchorStyles.Left;remove.ForeColor=Color.Firebrick;componentActions.Controls.Add(remove);
        requirements=new LinkLabel{Name="wifi-requirements",Text="查看配置要求",LinkColor=SettingsWindow.Accent,TextAlign=ContentAlignment.MiddleLeft};Add(requirements,5);
        feedback=new Label{Name="wifi-feedback",ForeColor=SettingsWindow.Muted,Padding=new(0,8,0,0)};Add(feedback,6);
        feedback.TextChanged+=(_,_)=>{if(IsHandleCreated&&!IsDisposed)PaintState();};
        var footer=UnlockSettingsStyle.Footer();outer.Controls.Add(footer,0,1);
        test=UnlockSettingsStyle.Action("wifi-check","检查连接",100);footer.Controls.Add(test,0,0);
        save=UnlockSettingsStyle.Action("wifi-save","保存",108,true);footer.Controls.Add(save,2,0);
        save.Click+=(_,_)=>Save();test.Click+=async(_,_)=>await CheckAsync();action.Click+=async(_,_)=>await ChangeAsync(nextAction);
        remove.Click+=async(_,_)=>{if(ConfirmRemoval())await ChangeAsync("uninstall");};
        requirements.LinkClicked+=(_,_)=>MessageBox.Show(this,snapshot.Detail+"\n\n"+TightVncPackage.Requirements,"现有 TightVNC 配置",MessageBoxButtons.OK,MessageBoxIcon.Information);
        Shown+=async(_,_)=>await RefreshAsync();
        FormClosing+=(_,e)=>{if(busy){e.Cancel=true;feedback.Text="组件正在处理，请完成后关闭。";}else closed.Cancel();};FormClosed+=(_,_)=>closed.Dispose();
        PaintState();UnlockSettingsStyle.Complete(this);
    }
    private bool HasSavedPassword() {
        byte[] bytes=[];try{bytes=store.Read().Password;return WifiKeyboardAccessStore.Usable(bytes);}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException){return false;}
        finally{CryptographicOperations.ZeroMemory(bytes);}
    }
    private void PaintState() {
        bool saved=HasSavedPassword();float scale=IsHandleCreated?DeviceDpi/96f:1f;
        componentState.Text=snapshot.Kind switch{TightVncKind.Missing=>"未安装",TightVncKind.Managed=>snapshot.Running?(saved?"已就绪":"待自动配置"):"服务未启动",TightVncKind.Existing=>snapshot.Compatible?(saved?"已接入已有安装":"待自动接入"):"需检查设置",TightVncKind.RestartRequired=>"等待重启",TightVncKind.CleanupRequired=>"等待清理",_=>"无法自动管理"};
        nextAction=snapshot.Kind==TightVncKind.Missing?"install":snapshot.NeedsAuthorization?"authorize":(snapshot.Kind is TightVncKind.Managed or TightVncKind.Existing)&&snapshot.Compatible?(!snapshot.Running?"start":snapshot.Kind==TightVncKind.Existing||!saved?"authorize":""):"";
        action.Text=nextAction=="install"?"安装并启用":nextAction=="start"?"启动并接入":saved?"重新接入":"自动接入";action.Visible=nextAction.Length>0;
        remove.Visible=snapshot.CanRemove;remove.Text=snapshot.Kind==TightVncKind.CleanupRequired?"完成卸载":"卸载组件";requirements.Visible=snapshot.Kind==TightVncKind.Conflict||snapshot.Kind==TightVncKind.Existing&&!snapshot.Compatible;
        int[] heights=[44,44,12,24,44,requirements.Visible?28:0,feedback.Text.Length>0?48:20];for(int i=0;i<heights.Length;i++)body.RowStyles[i].Height=heights[i]*scale;
        body.Height=(int)Math.Ceiling(body.RowStyles.Cast<RowStyle>().Sum(row=>row.Height));ClientSize=new(ClientSize.Width,body.Height+(int)(108*scale));SetBusy(busy);
    }
    private void SetBusy(bool value){busy=value;action.Enabled=remove.Enabled=enabled.Enabled=test.Enabled=save.Enabled=requirements.Enabled=!value;UseWaitCursor=value;}
    private Task UiAsync(Action update) {
        if(IsDisposed||Disposing||lifetime.IsCancellationRequested||!IsHandleCreated)return Task.CompletedTask;
        if(!InvokeRequired){update();return Task.CompletedTask;}
        var done=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        try{BeginInvoke((Action)(()=>{try{if(!IsDisposed&&!Disposing&&!lifetime.IsCancellationRequested)update();done.SetResult();}catch(Exception ex){done.SetException(ex);}}));}
        catch(InvalidOperationException){done.SetResult();}
        return done.Task;
    }
    private async Task RefreshAsync() {
        if(IsDisposed||Disposing||lifetime.IsCancellationRequested)return;
        try{var state=await component.InspectAsync(lifetime);await UiAsync(()=>{snapshot=state;PaintState();});}
        catch(OperationCanceledException){}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){await UiAsync(()=>{snapshot=new(TightVncKind.Conflict,Detail:"组件检查未完成，请重试。");PaintState();});}
    }
    private void Save() {
        byte[] password=[];
        try {
            if(!canEdit()){feedback.Text="请先解锁电脑。";return;}password=store.Read().Password;
            if(enabled.Checked&&!WifiKeyboardAccessStore.Usable(password)){feedback.Text=snapshot.Kind==TightVncKind.Missing?"请先安装并启用组件。":"请先自动接入组件。";return;}
            if(!enabled.Checked&&password.Length==0){feedback.Text="Wi-Fi 解锁已关闭。";return;}
            store.SaveExisting(enabled.Checked,password);feedback.Text=enabled.Checked?"已保存。":"已关闭 Wi-Fi 解锁。";
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException or ArgumentException){feedback.Text="未保存，请重新检查组件。";}
        finally{CryptographicOperations.ZeroMemory(password);}
    }
    private async Task CheckAsync() {
        if(enabled.Checked!=store.Enabled){feedback.Text="请先保存设置。";return;}SetBusy(true);feedback.Text="正在检查…";
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(lifetime);timeout.CancelAfter(4000);
        try{await check(timeout.Token);await UiAsync(()=>feedback.Text="连接正常。");}
        catch(Exception ex) when(ex is IOException or System.Net.Sockets.SocketException or UnauthorizedAccessException or CryptographicException or OperationCanceledException or ArgumentException){await UiAsync(()=>feedback.Text="连接失败，请重新接入或检查组件。");}
        finally{await UiAsync(()=>SetBusy(false));await RefreshAsync();}
    }
    private async Task ChangeAsync(string operation) {
        if(busy||operation.Length==0)return;if(!canEdit()){feedback.Text="请先解锁电脑。";return;}SetBusy(true);feedback.Text=operation=="uninstall"?"正在卸载，请处理管理员提示…":"正在配置，请处理管理员提示…";
        try{var result=await component.ChangeAsync(operation,snapshot.Instance,lifetime);await UiAsync(()=>{feedback.Text=result.Message;enabled.Checked=store.Enabled;});}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or OperationCanceledException or System.ComponentModel.Win32Exception){await UiAsync(()=>feedback.Text="操作未完成，请重新检查组件。");}
        finally{await UiAsync(()=>SetBusy(false));await RefreshAsync();}
    }
}
