using System.Security.Cryptography;
using System.Security.Principal;

namespace AIBotBridge;

internal enum TightVncKind { Missing, Managed, Existing, Conflict, RestartRequired, CleanupRequired }
internal sealed record TightVncState(TightVncKind Kind,bool Running=false,bool Compatible=false,string Instance="",string Detail="",bool NeedsAuthorization=false)
{
    internal bool CanRemove=>Kind is TightVncKind.Managed or TightVncKind.CleanupRequired;
}
internal sealed record TightVncResult(bool Ok,string Message,bool RestartRequired=false,bool KeepCredentials=false,string Diagnostic="");
internal interface ITightVncController
{
    Task<TightVncState> InspectAsync(CancellationToken token);
    Task<TightVncResult> ChangeAsync(string action,string instance,CancellationToken token);
}

internal static class TightVncPackage
{
    internal const string Version="2.8.90.0",FileName="tightvnc-2.8.90-gpl-setup-64bit.msi";
    internal const string Sha256="9693cb4cdc2e7e482e8e3f5890415b57ef87de3369fa5c337db516218a1170ce";
    internal const string SourceSha256="f3f76393a5865e1f1919c78c3eb43b9c937cf738927c2bfc920ecbd3913b061c";
    internal const string ProductCode="{AFF73242-938E-4D86-B133-6A287F7E4A04}";
    internal static string DirectoryPath=>Path.Combine(AppContext.BaseDirectory,"components","tightvnc");
    internal static string Installer=>Path.Combine(DirectoryPath,FileName);
    internal static readonly IReadOnlyDictionary<string,int> Options=new Dictionary<string,int> {
        ["AllowLoopback"]=1,["LoopbackOnly"]=1,["UseVncAuthentication"]=1,
        ["AcceptHttpConnections"]=0,["LogLevel"]=0,["AcceptRfbConnections"]=1,
        ["BlockRemoteInput"]=0,["DisconnectAction"]=0,["UseMirrorDriver"]=0,["RfbPort"]=5900
    };
    internal static bool Successful(uint code)=>code is 0 or 3010;
    internal static string Hash(string path) {using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();}
    internal static byte[] NewSecret() {
        const string alphabet="ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
        byte[] result=new byte[8];
        for(int i=0;i<result.Length;i++)result[i]=(byte)alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
        result[0]=(byte)alphabet[RandomNumberGenerator.GetInt32(48)];return result;
    }
    internal static string Sid=>WindowsIdentity.GetCurrent().User?.Value??throw new IOException("无法确认当前 Windows 用户。");
    internal const string Requirements="TightVNC Server 须作为 LocalSystem 服务运行；只允许本机连接，启用密码认证，关闭 Web 访问、日志和断开后锁屏，允许键盘输入。现有设置将保留，请由管理员在 TightVNC 中调整后重新检查。";
}

// A failed setup restores only this caller's previous encrypted access record.
// Neither PIN/pairing files nor another user's profile are setup inputs.
internal sealed class TightVncController(WifiKeyboardAccessStore? access=null) : ITightVncController
{
    private readonly WifiKeyboardAccessStore store=access??new();
    internal Func<bool> CanEdit {get;set;}=()=>WindowsSessionLock.Read()==false;
    internal Func<CancellationToken,Task> Check {get;set;}=async ct=>{using var connection=await WifiKeyboardRfb.ConnectAsync(ct,initialize:false);};
    internal Func<string,string,byte[],Func<bool,Task<bool>>,CancellationToken,Task<TightVncResult>> Elevate {get;set;}=TightVncElevation.RunAsync;
    internal Func<byte[]> ReadAccess {get;set;}=TightVncMachine.ReadAccess;
    public Task<TightVncState> InspectAsync(CancellationToken token)=>Task.Run(()=>TightVncMachine.Inspect(TightVncPackage.Sid),token);
    public async Task<TightVncResult> ChangeAsync(string action,string instance,CancellationToken token) {
        if(action is not ("install" or "start" or "authorize" or "uninstall"))throw new ArgumentException("未知组件操作。");
        if(!CanEdit())return new(false,"请先解锁电脑。",false);
        byte[] secret=[],previous=[];bool hadPrevious=store.Exists,changed=false;
        try {
            if(action=="install")secret=TightVncPackage.NewSecret();
            if(action is "install" or "start" or "authorize")previous=store.EncryptedSnapshot();
            if(action=="uninstall") {
                previous=store.EncryptedSnapshot();
                var current=store.Read();
                try{if(WifiKeyboardAccessStore.Usable(current.Password)){store.SaveExisting(false,current.Password);changed=true;}}
                finally{CryptographicOperations.ZeroMemory(current.Password);}
            }
            var result=await Elevate(action,instance,secret,async restart=> {
                if(action!="install")return true;
                store.Save(!restart,secret);changed=true;
                if(restart)return true;
                using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(4000);
                await Check(timeout.Token);return true;
            },token);
            if(action=="install"&&!result.Ok&&result.KeepCredentials){store.Save(false,secret);changed=true;}
            if(result.Ok&&action is "start" or "authorize") {
                byte[] existing=ReadAccess();
                try {
                    store.SaveExisting(true,existing);changed=true;
                    using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(4000);await Check(timeout.Token);
                    return new(true,"已自动接入，原配置和密码保留。");
                }finally{CryptographicOperations.ZeroMemory(existing);}
            }
            if(action=="install"&&!result.Ok&&!result.KeepCredentials&&changed)store.RestoreEncrypted(previous,hadPrevious);
            if(result.Ok&&action=="uninstall")store.Delete();
            return result;
        }catch(System.ComponentModel.Win32Exception ex) when(ex.NativeErrorCode==1223){if(changed)store.RestoreEncrypted(previous,hadPrevious);return new(false,"已取消管理员授权，未完成操作。");}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or CryptographicException or OperationCanceledException or System.ComponentModel.Win32Exception or System.Net.Sockets.SocketException) {
            if(changed)store.RestoreEncrypted(previous,hadPrevious);
            return new(false,ex is OperationCanceledException?"操作已取消或超时，请重新检查组件状态。":"操作未完成，请重新检查组件状态；原有 PIN 和配对保留。",Diagnostic:$"{ex.GetType().Name}/0x{ex.HResult:x8}");
        }finally{CryptographicOperations.ZeroMemory(secret);CryptographicOperations.ZeroMemory(previous);}
    }
}
