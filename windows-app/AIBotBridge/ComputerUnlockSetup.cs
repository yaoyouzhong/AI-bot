using System.Security.Cryptography;

namespace AIBotBridge;

internal sealed record UnlockSetupStatus(bool MethodReady,bool WifiReady,string Message="")
{
    internal bool Ready=>MethodReady&&WifiReady;
}

// One setup path for installation and the single settings page. No Windows
// credential is an input; the existing controller owns its separate VNC secret.
internal sealed class ComputerUnlockSetup
{
    private readonly WifiKeyboardAccessStore access;
    private readonly ITightVncController component;
    private readonly Func<bool> helper;
    private readonly Func<Task<bool>> installHelper;
    private readonly Func<CancellationToken,Task> check;
    internal ComputerUnlockSetup(WifiKeyboardAccessStore? access=null,ITightVncController? component=null,
        Func<bool>? helper=null,Func<Task<bool>>? installHelper=null,Func<CancellationToken,Task>? check=null)
    {
        this.access=access??new();this.component=component??new TightVncController(this.access);
        this.helper=helper??(()=>WindowsLoginMethod.Installed);this.installHelper=installHelper??(()=>WindowsLoginMethod.ConfigureAsync(true));
        this.check=check??(async ct=>{using var connection=await WifiKeyboardRfb.ConnectAsync(ct,initialize:false);});
    }
    private bool HasAccess()
    {
        byte[] password=[];
        try{password=access.Read().Password;return WifiKeyboardAccessStore.Usable(password);}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException){return false;}
        finally{CryptographicOperations.ZeroMemory(password);}
    }
    internal async Task<UnlockSetupStatus> InspectAsync(CancellationToken token)
    {
        bool method=helper();if(!access.Enabled||!HasAccess())return new(method,false,"Wi-Fi 尚未配置。");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(4000);
        try{await check(deadline.Token);return new(method,true);}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException or OperationCanceledException or System.Net.Sockets.SocketException){return new(method,false,"Wi-Fi 尚未就绪。");}
    }
    internal async Task<UnlockSetupStatus> EnsureAsync(Action<string> progress,CancellationToken token)
    {
        try {
            if(!helper()) {
                progress("正在配置登录方式，请完成 Windows 管理员确认…");
                if(!await installHelper())return new(false,false,"登录方式组件未完成配置，请点击保存重试。");
            }
            token.ThrowIfCancellationRequested();
            progress("正在配置 Wi-Fi 解锁…");
            var state=await component.InspectAsync(token);
            string action=state.Kind==TightVncKind.Missing?"install":state.NeedsAuthorization?"authorize":
                state.Kind is TightVncKind.Managed or TightVncKind.Existing && state.Compatible ? !state.Running?"start":!HasAccess()?"authorize":"" : "";
            if(state.Kind is TightVncKind.Conflict or TightVncKind.RestartRequired or TightVncKind.CleanupRequired||
                state.Kind==TightVncKind.Existing&&!state.Compatible)return new(true,false,"Wi-Fi 配置未完成："+state.Detail);
            if(action.Length>0) {
                progress("正在配置 Wi-Fi，请完成 Windows 管理员确认…");
                var result=await component.ChangeAsync(action,state.Instance,token);
                if(!result.Ok||result.RestartRequired)return new(true,false,result.Message);
            }
            byte[] password=[];byte[] previous=access.EncryptedSnapshot();bool had=access.Exists,changed=false;
            try {
                password=access.Read().Password;
                if(!WifiKeyboardAccessStore.Usable(password))return new(true,false,"Wi-Fi 接入未完成，请点击保存重试。");
                if(!access.Enabled){access.SaveExisting(true,password);changed=true;}
                progress("正在检查 USB／蓝牙和 Wi-Fi 解锁…");
                var status=await InspectAsync(token);
                if(!status.Ready&&changed)access.RestoreEncrypted(previous,had);
                return status.Ready?status:new(status.MethodReady,false,"Wi-Fi 连接检查未通过，请点击保存重试。");
            }finally{CryptographicOperations.ZeroMemory(password);CryptographicOperations.ZeroMemory(previous);}
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException or OperationCanceledException or System.ComponentModel.Win32Exception or System.Net.Sockets.SocketException){return new(helper(),false,"解锁配置未完成，请点击保存重试。");}
    }
}
