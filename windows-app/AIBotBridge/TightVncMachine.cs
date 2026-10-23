using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal sealed record TightVncReceipt(string Owner,string Instance,string FileHash,string ConfigHash,string State="ready");
internal static class TightVncMachine
{
    private const string ConfigPath=@"SOFTWARE\TightVNC\Server",ReceiptPath=@"SOFTWARE\AI-bot\Components\TightVNC";
    internal static string ServerPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"TightVNC","tvnserver.exe");
    internal sealed record Service(uint Type,uint State,string Command,string Account);
    internal static bool TrustedService(Service service)=>service.Type==0x10&&
        service.Command.Equals('"'+ServerPath+"\" -service",StringComparison.OrdinalIgnoreCase)&&
        service.Account.Equals("LocalSystem",StringComparison.OrdinalIgnoreCase)&&File.Exists(ServerPath)&&VerifyPublisher(ServerPath);
    internal static TightVncState Inspect(string sid) {
        try {
            var service=ReadService();var receipt=ReadReceipt();
            using var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
            RegistryKey? opened;
            try{opened=root.OpenSubKey(ConfigPath);}
            catch(Exception ex) when(ex is UnauthorizedAccessException or System.Security.SecurityException) {
                if(service is not null&&TrustedService(service))return new(TightVncKind.Existing,service.State==4,Detail:"需要管理员检查本机访问权限，现有配置和密码保留。",NeedsAuthorization:true);
                throw;
            }
            using var config=opened;
            bool product=Native.MsiQueryProductState(TightVncPackage.ProductCode)==5;
            bool remnants=service is not null||config is not null||File.Exists(ServerPath)||product;
            if(receipt?.State=="removing") {
                if(receipt.Owner==sid&&service is null&&!product&&!File.Exists(ServerPath)&&config is not null&&Fingerprint(config)==receipt.ConfigHash)
                    return new(TightVncKind.CleanupRequired,Instance:receipt.Instance,Detail:"组件已移除，需要管理员完成本机配置清理。");
                if(service is null&&!product&&!File.Exists(ServerPath)&&config is not null)
                    return new(TightVncKind.Conflict,Detail:"组件已移除，但留下的配置已变化，原设置保留。");
                return remnants?new(TightVncKind.RestartRequired,Detail:"卸载等待 Windows 重启完成。"):new(TightVncKind.Missing);
            }
            if(!remnants)return new(TightVncKind.Missing);
            if(service is null||!TrustedService(service))return new(TightVncKind.Conflict,Detail:"检测到已有安装或残留，无法核实官方服务；不会覆盖或卸载。");
            bool compatible=Compatible(config);
            if(receipt is not null&&receipt.Owner==sid) {
                bool same=product&&receipt.FileHash==TightVncPackage.Hash(ServerPath)&&receipt.ConfigHash==Fingerprint(config);
                if(!same)return new(TightVncKind.Conflict,Detail:"组件在安装后已被修改或升级，请通过 Windows 设置管理现有安装。");
                return new(TightVncKind.Managed,service.State==4,compatible,receipt.Instance,receipt.State=="restart"?"组件需要重启或完成连接检查。":"由 AI-bot 安装和管理。");
            }
            return new(TightVncKind.Existing,service.State==4,compatible,Detail:compatible?"使用已有 TightVNC；原配置保留。":TightVncPackage.Requirements);
        }catch(Exception ex) when(ex is UnauthorizedAccessException or System.Security.SecurityException or IOException or Win32Exception or JsonException) {
            return new(TightVncKind.Conflict,Detail:"无法读取或核实已有组件。请检查管理员设置，现有安装不会被覆盖。");
        }
    }
    internal static bool Compatible(Func<string,int> read)=>TightVncPackage.Options
        .Where(p=>p.Key is not ("RfbPort" or "UseMirrorDriver")).All(p=>read(p.Key)==p.Value)&&read("RfbPort") is >=1024 and <=65535;
    private static bool Compatible(RegistryKey? key)=>key is not null&&Compatible(name=>key.GetValue(name) is int n?n:-1);
    internal static byte[] ReadAccess() {
        var state=Inspect(TightVncPackage.Sid);
        if(state.Kind is not (TightVncKind.Existing or TightVncKind.Managed)||!state.Compatible)throw new IOException("现有组件无法安全自动接入，原设置保留。");
        using var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);using var config=root.OpenSubKey(ConfigPath);
        if(config?.GetValue("Password") is not byte[] encrypted||encrypted.Length!=8)throw new IOException("现有组件没有可接入的访问认证。");
        try{return DecodeAccess(encrypted);}finally{CryptographicOperations.ZeroMemory(encrypted);}
    }
    // Independently implemented storage interoperability using .NET DES. This
    // fixed public VNC storage key is an encoding constant, not a user secret.
    // Only a verified compatible local service is read; no password is shown.
    internal static byte[] DecodeAccess(byte[] encrypted) {
        if(encrypted.Length!=8)throw new IOException("现有组件认证记录无效。");
        byte[] key=[232,74,214,96,196,114,26,224];
        try{using var des=DES.Create();des.Key=key;var password=des.DecryptEcb(encrypted,PaddingMode.None);if(WifiKeyboardAccessStore.Usable(password))return password;CryptographicOperations.ZeroMemory(password);throw new IOException("现有组件未启用可用的访问认证。");}
        finally{CryptographicOperations.ZeroMemory(key);}
    }
    private static string Fingerprint(RegistryKey? key) {
        if(key is null)return "";
        object Read(RegistryKey current,int depth) {
            if(depth>8||current.ValueCount>256||current.SubKeyCount>64)throw new IOException("组件配置超出检查范围。");
            var values=current.GetValueNames().OrderBy(x=>x,StringComparer.Ordinal).Select(name=>new{name,kind=current.GetValueKind(name).ToString(),value=current.GetValue(name, null,RegistryValueOptions.DoNotExpandEnvironmentNames)}).ToArray();
            var children=current.GetSubKeyNames().OrderBy(x=>x,StringComparer.Ordinal).Select(name=>{using var child=current.OpenSubKey(name)??throw new IOException("组件配置已变化。");return new{name,values=Read(child,depth+1)};}).ToArray();
            return new{values,children};
        }
        byte[] data=JsonSerializer.SerializeToUtf8Bytes(Read(key,0));
        try{return Convert.ToHexString(SHA256.HashData(data));}finally{CryptographicOperations.ZeroMemory(data);}
    }
    private static string AuthenticationFingerprint(RegistryKey key) {
        var values=TightVncPackage.Options.Keys.Concat(new[]{"Password","ControlPassword","UseControlAuthentication","RunControlInterface"})
            .OrderBy(x=>x,StringComparer.Ordinal).Select(name=>new{name,value=key.GetValue(name)}).ToArray();
        byte[] data=JsonSerializer.SerializeToUtf8Bytes(values);
        try{return Convert.ToHexString(SHA256.HashData(data));}finally{CryptographicOperations.ZeroMemory(data);}
    }
    private static TightVncReceipt? ReadReceipt() {
        using var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
        using var key=root.OpenSubKey(ReceiptPath,RegistryKeyPermissionCheck.ReadSubTree,RegistryRights.ReadKey|RegistryRights.ReadPermissions);
        if(key is null)return null;
        // An unprivileged marker never authorizes service changes or removal.
        var acl=key.GetAccessControl(AccessControlSections.Access);
        foreach(RegistryAccessRule rule in acl.GetAccessRules(true,true,typeof(SecurityIdentifier))) {
            var identity=(SecurityIdentifier)rule.IdentityReference;
            const RegistryRights write=RegistryRights.SetValue|RegistryRights.CreateSubKey|RegistryRights.Delete|RegistryRights.ChangePermissions|RegistryRights.TakeOwnership;
            if(rule.AccessControlType==AccessControlType.Allow&&(rule.RegistryRights&write)!=0&&
               !identity.IsWellKnown(WellKnownSidType.LocalSystemSid)&&!identity.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid))throw new IOException("组件归属登记权限不可信。");
        }
        string json=key.GetValue("Receipt") as string??"";
        if(json.Length>4096)throw new IOException("组件归属登记无效。");
        var record=JsonSerializer.Deserialize<TightVncReceipt>(json)??throw new IOException("组件归属登记无效。");
        if(!Guid.TryParseExact(record.Instance,"N",out _)||record.FileHash.Length!=64||record.ConfigHash.Length!=64||record.State is not ("ready" or "restart" or "removing"))throw new IOException("组件归属登记无效。");
        return record;
    }
    private static void WriteReceipt(TightVncReceipt receipt) {
        using var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
        using var key=root.CreateSubKey(ReceiptPath,RegistryKeyPermissionCheck.ReadWriteSubTree);
        var acl=new RegistrySecurity();acl.SetAccessRuleProtection(true,false);
        acl.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null));
        foreach(var identity in new[]{WellKnownSidType.LocalSystemSid,WellKnownSidType.BuiltinAdministratorsSid})
            acl.AddAccessRule(new(new SecurityIdentifier(identity,null),RegistryRights.FullControl,InheritanceFlags.None,PropagationFlags.None,AccessControlType.Allow));
        acl.AddAccessRule(new(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null),RegistryRights.ReadKey,InheritanceFlags.None,PropagationFlags.None,AccessControlType.Allow));
        key.SetAccessControl(acl);key.SetValue("Receipt",JsonSerializer.Serialize(receipt));
    }
    private static void RemoveReceipt() {using var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);root.DeleteSubKey(ReceiptPath,false);}
    private static void AllowRead(string sid) {
        using var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
        using var key=root.OpenSubKey(ConfigPath,RegistryKeyPermissionCheck.ReadWriteSubTree,RegistryRights.ReadPermissions|RegistryRights.ChangePermissions)??throw new IOException("组件配置不存在。");
        var acl=key.GetAccessControl(AccessControlSections.Access);
        acl.AddAccessRule(new(new SecurityIdentifier(sid),RegistryRights.ReadKey,InheritanceFlags.None,PropagationFlags.None,AccessControlType.Allow));key.SetAccessControl(acl);
    }
    internal static async Task<TightVncResult> ChangeAsync(string action,string sid,string instance,byte[] password,Func<bool,Task<bool>> commit) {
        using var identity=WindowsIdentity.GetCurrent();
        if(!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))throw new UnauthorizedAccessException();
        string? staged=null;string stage="inspect";bool installed=false,accepted=false;TightVncReceipt? receipt=null;
        using var ownership=new Semaphore(1,1,@"Global\AI-bot-TightVNC-setup-v1");
        if(!ownership.WaitOne(0))return new(false,"另一个组件安装操作正在进行，请稍后重试。");
        try {
            var before=Inspect(sid);
            if(action=="install") {
                if(before.Kind!=TightVncKind.Missing||password.Length!=8||!WifiKeyboardAccessStore.Valid(password))return new(false,"检测到已有组件，请先检查现有安装。");
                if(ReadReceipt() is {} stale&&stale.Owner!=sid)return new(false,"检测到其他用户的组件登记，请由管理员检查。");
                stage="verify-payload";staged=StageInstaller();
                using(var policy=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows\Installer"))
                    if(policy?.GetValue("Debug") is int debug&&(debug&7)==7)throw new IOException("系统安装调试策略不允许安全传递组件密码。");
                Native.MsiSetInternalUI(2,IntPtr.Zero);
                byte[] admin=TightVncPackage.NewSecret();uint code;
                try {stage="msi-install";code=Native.MsiInstallProduct(staged,InstallProperties(password,admin));}
                finally{CryptographicOperations.ZeroMemory(admin);}
                if(!TightVncPackage.Successful(code))return new(false,$"组件安装未完成（{code}），请重新检查状态。",Diagnostic:stage+"/"+code);
                installed=true;
                stage="verify-service";var service=ReadService();
                using var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);using var config=root.OpenSubKey(ConfigPath);
                if(service is null||!TrustedService(service)||!Compatible(config)||!TightVncPackage.Options.All(p=>config!.GetValue(p.Key) is int n&&n==p.Value))throw new IOException("安装后组件验证未通过。");
                receipt=new(sid,Guid.NewGuid().ToString("N"),TightVncPackage.Hash(ServerPath),Fingerprint(config),code==3010?"restart":"ready");
                string authentication=AuthenticationFingerprint(config!);
                stage="record-ownership";WriteReceipt(receipt);
                AllowRead(sid);
                if(code==0&&service.State!=4)StartService();
                stage="verify-caller-access";accepted=await commit(code==3010);
                if(accepted) {
                    // The official service can populate remaining defaults on
                    // its first authenticated connection. Finalize ownership
                    // after that connection, while protecting auth/network
                    // settings and the exact installed binary throughout.
                    if(authentication!=AuthenticationFingerprint(config!)||ReadService() is not {} verified||!TrustedService(verified)||receipt.FileHash!=TightVncPackage.Hash(ServerPath)) {
                        accepted=false;return new(false,"组件在配置期间已变化，原安装保留。",KeepCredentials:true,Diagnostic:"changed-during-setup");
                    }
                    WriteReceipt(receipt with{ConfigHash=Fingerprint(config)});
                    return new(true,code==3010?"组件已安装，请重启电脑后检查连接。":"组件已安装并启用。",code==3010);
                }
                bool clean=Rollback(receipt);installed=false;
                return new(false,clean?"配置未完成，本次安装已回退。":"配置未完成，组件仍保留；请检查组件后再重试。",KeepCredentials:!clean,Diagnostic:stage);
            }
            if(action=="uninstall") {
                if(!before.CanRemove||before.Instance!=instance)return new(false,"归属或组件内容已变化，不会卸载现有 TightVNC。");
                receipt=ReadReceipt()!;
                if(before.Kind==TightVncKind.CleanupRequired) {
                    if(!FinishRemoval(receipt))return new(false,"组件配置已变化，未清理现有设置。");
                    return new(true,"组件卸载已完成。");
                }
                Native.MsiSetInternalUI(2,IntPtr.Zero);
                uint code=Native.MsiConfigureProductEx(TightVncPackage.ProductCode,0,2,"REBOOT=ReallySuppress");
                if(!TightVncPackage.Successful(code))return new(false,$"卸载未完成（{code}），组件访问已保留，请检查状态。");
                if(code==3010){WriteReceipt(receipt with{State="removing"});return new(true,"组件卸载需要重启；Wi-Fi 解锁已停用。",true);}
                if(ReadService() is not null||Native.MsiQueryProductState(TightVncPackage.ProductCode)==5)throw new IOException("卸载后组件仍存在。");
                if(!FinishRemoval(receipt))return new(false,"组件已移除，本机配置未清理，请重新检查状态。");
                return new(true,"解锁组件已卸载，Windows PIN 和配对保留。");
            }
            if(action is "start" or "authorize") {
                if(before.Kind is not (TightVncKind.Managed or TightVncKind.Existing)||!before.Compatible||
                   before.Kind==TightVncKind.Managed&&before.Instance!=instance)return new(false,"现有配置不兼容或已变化，原设置保留。");
                if(action=="start")StartService();
                AllowRead(sid);return new(true,action=="start"?"组件服务已启动，请检查连接。":"已允许当前用户读取组件配置，原配置和密码保留。");
            }
            throw new ArgumentException("未知组件操作。");
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or Win32Exception or System.Security.SecurityException or OperationCanceledException or CryptographicException) {
            bool retained=installed&&!(receipt is not null&&Rollback(receipt));installed=false;
            return new(false,retained?"安装未完成，组件可能仍保留，请检查安装状态。":"组件操作未完成，请重新检查状态。",KeepCredentials:retained,Diagnostic:stage+"/"+ex.GetType().Name+"/0x"+ex.HResult.ToString("X8"));
        }finally {
            if(installed&&!accepted) {
                // Only this fresh, unchanged install may be rolled back. Never
                // remove a pre-existing or subsequently modified service.
                try {
                    if(receipt is not null)Rollback(receipt);
                }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or Win32Exception) { /* Re-inspection exposes the retained install; never claim rollback success. */ }
            }
            if(staged is not null){try{File.Delete(staged);Directory.Delete(Path.GetDirectoryName(staged)!);}catch(IOException){}}
            ownership.Release();
        }
    }
    private static bool Rollback(TightVncReceipt receipt) {
        try {
            if(Inspect(receipt.Owner) is not {Kind:TightVncKind.Managed,Instance:var current}||current!=receipt.Instance)return false;
            uint code=Native.MsiConfigureProductEx(TightVncPackage.ProductCode,0,2,"REBOOT=ReallySuppress");
            if(!TightVncPackage.Successful(code))return false;
            if(code==3010){WriteReceipt(receipt with{State="removing"});return false;}
            if(ReadService() is not null||Native.MsiQueryProductState(TightVncPackage.ProductCode)==5)return false;
            return FinishRemoval(receipt);
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or Win32Exception or System.Security.SecurityException){return false;}
    }
    private static bool FinishRemoval(TightVncReceipt receipt) {
        if(ReadService() is not null||File.Exists(ServerPath)||Native.MsiQueryProductState(TightVncPackage.ProductCode)==5)return false;
        using var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
        // Official MSI intentionally retains server settings. Delete only this
        // installation's exact unchanged configuration after its service is gone.
        using(var config=root.OpenSubKey(ConfigPath))if(config is not null&&Fingerprint(config)!=receipt.ConfigHash){WriteReceipt(receipt with{State="removing"});return false;}
        root.DeleteSubKeyTree(ConfigPath,false);
        RemoveReceipt();return true;
    }
    private static string InstallProperties(byte[] password,byte[] admin) {
        var settings=new List<string>{"ADDLOCAL=Server","REBOOT=ReallySuppress","SERVER_REGISTER_AS_SERVICE=1","SERVER_ADD_FIREWALL_EXCEPTION=0","VIEWER_ADD_FIREWALL_EXCEPTION=0","SERVER_ALLOW_SAS=0"};
        var values=TightVncPackage.Options.Concat(new Dictionary<string,int>{{"RunControlInterface",0},{"UseControlAuthentication",1}});
        foreach(var option in values){string key=option.Key.ToUpperInvariant();settings.Add("SET_"+key+"=1");settings.Add("VALUE_OF_"+key+"="+option.Value);}
        settings.Add("SET_PASSWORD=1");settings.Add("VALUE_OF_PASSWORD="+Encoding.ASCII.GetString(password));
        settings.Add("SET_CONTROLPASSWORD=1");settings.Add("VALUE_OF_CONTROLPASSWORD="+Encoding.ASCII.GetString(admin));
        settings.Add("MsiHiddenProperties=ExecSecureObjects_64;ExecSecureObjectsRollback_64;VALUE_OF_PASSWORD;VALUE_OF_CONTROLPASSWORD;VALUE_OF_VIEWONLYPASSWORD;SetPasswordsActionSilently;SetControlPasswordsActionSilently;SetViewOnlyPasswordsActionSilently;CustomActionData");
        return string.Join(' ',settings);
    }
    private static string StageInstaller() {
        string source=TightVncPackage.Installer;
        using var locked=File.Open(source,FileMode.Open,FileAccess.Read,FileShare.Read);
        if(Convert.ToHexString(SHA256.HashData(locked)).ToLowerInvariant()!=TightVncPackage.Sha256||!VerifyPublisher(source))throw new IOException("组件安装包损坏或签名无效。");
        string parent=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"AI-bot-ComponentSetup");
        if(Directory.Exists(parent)&&(File.GetAttributes(parent)&FileAttributes.ReparsePoint)!=0)throw new IOException("安装缓存路径不可用。");
        Directory.CreateDirectory(parent);var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);
        acl.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null));
        foreach(var sid in new[]{WellKnownSidType.LocalSystemSid,WellKnownSidType.BuiltinAdministratorsSid})acl.AddAccessRule(new(new SecurityIdentifier(sid,null),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        new DirectoryInfo(parent).SetAccessControl(acl);
        string folder=Path.Combine(parent,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        string target=Path.Combine(folder,TightVncPackage.FileName);locked.Position=0;
        using(var output=File.Create(target))locked.CopyTo(output);
        if(TightVncPackage.Hash(target)!=TightVncPackage.Sha256||!VerifyPublisher(target))throw new IOException("组件安装包复核失败。");
        return target;
    }
    internal static Service? ReadService() {
        using var manager=Native.OpenSCManager(null,null,1);if(manager.IsInvalid)throw new Win32Exception();
        using var service=Native.OpenService(manager,"tvnserver",5);
        if(service.IsInvalid){if(Marshal.GetLastWin32Error()==1060)return null;throw new Win32Exception();}
        IntPtr buffer=Marshal.AllocHGlobal(8192);
        try {
            if(!Native.QueryServiceConfig(service,buffer,8192,out _)||!Native.QueryServiceStatusEx(service,0,out var status,Marshal.SizeOf<ServiceStatus>(),out _))throw new Win32Exception();
            var config=Marshal.PtrToStructure<ServiceConfig>(buffer);
            return new(config.Type,status.State,Marshal.PtrToStringUni(config.Command)??"",Marshal.PtrToStringUni(config.Account)??"");
        }finally{Marshal.FreeHGlobal(buffer);}
    }
    private static void StartService() {
        using var manager=Native.OpenSCManager(null,null,1);using var service=Native.OpenService(manager,"tvnserver",0x14);
        if(service.IsInvalid)throw new Win32Exception();
        if(!Native.StartService(service,0,IntPtr.Zero)&&Marshal.GetLastWin32Error()!=1056)throw new Win32Exception();
        for(int i=0;i<40;i++){if(ReadService()?.State==4)return;Thread.Sleep(250);}throw new IOException("组件服务未能启动。");
    }
    internal static bool VerifyPublisher(string path) {
        IntPtr file=IntPtr.Zero,filename=IntPtr.Zero;
        try {
            filename=Marshal.StringToCoTaskMemUni(path);file=Marshal.AllocCoTaskMem(Marshal.SizeOf<TrustFile>());
            Marshal.StructureToPtr(new TrustFile{Size=(uint)Marshal.SizeOf<TrustFile>(),Path=filename},file,false);
            var data=new TrustData{Size=(uint)Marshal.SizeOf<TrustData>(),Ui=2,Revocation=0,Choice=1,File=file,ProviderFlags=0x1000};
            var action=new Guid("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
            if(Native.WinVerifyTrust(new IntPtr(-1),ref action,ref data)!=0)return false;
            using var cert=new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
            return cert.GetNameInfo(X509NameType.SimpleName,false)=="OOO GlavSoft";
        }catch(Exception ex) when(ex is CryptographicException or IOException){return false;}
        finally{if(file!=IntPtr.Zero)Marshal.FreeCoTaskMem(file);if(filename!=IntPtr.Zero)Marshal.FreeCoTaskMem(filename);}
    }
    [StructLayout(LayoutKind.Sequential)]private struct TrustFile{internal uint Size;internal IntPtr Path,Handle,Subject;}
    [StructLayout(LayoutKind.Sequential)]private struct TrustData{internal uint Size;internal IntPtr Policy,Sip;internal uint Ui,Revocation,Choice;internal IntPtr File;internal uint StateAction;internal IntPtr State,Url;internal uint ProviderFlags,UiContext;internal IntPtr Signature;}
    [StructLayout(LayoutKind.Sequential)]private struct ServiceConfig{internal uint Type,StartType,Error;internal IntPtr Command,Group;internal uint Tag;internal IntPtr Dependencies,Account,Display;}
    [StructLayout(LayoutKind.Sequential)]private struct ServiceStatus{internal uint Type,State,Controls,Exit,SpecificExit,Checkpoint,Wait,Pid,Flags;}
    private sealed class ServiceHandle():SafeHandleZeroOrMinusOneIsInvalid(true){protected override bool ReleaseHandle()=>Native.CloseServiceHandle(handle);}
    private static class Native {
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]internal static extern ServiceHandle OpenSCManager(string? machine,string? database,uint rights);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]internal static extern ServiceHandle OpenService(ServiceHandle manager,string name,uint rights);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]internal static extern bool QueryServiceConfig(ServiceHandle service,IntPtr buffer,int size,out int needed);
        [DllImport("advapi32.dll",SetLastError=true)]internal static extern bool QueryServiceStatusEx(ServiceHandle service,int type,out ServiceStatus status,int size,out int needed);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]internal static extern bool StartService(ServiceHandle service,int count,IntPtr args);
        [DllImport("advapi32.dll")]internal static extern bool CloseServiceHandle(IntPtr handle);
        [DllImport("msi.dll",CharSet=CharSet.Unicode)]internal static extern uint MsiInstallProduct(string path,string properties);
        [DllImport("msi.dll",CharSet=CharSet.Unicode)]internal static extern uint MsiConfigureProductEx(string product,int level,int state,string properties);
        [DllImport("msi.dll",CharSet=CharSet.Unicode)]internal static extern int MsiQueryProductState(string product);
        [DllImport("msi.dll")]internal static extern uint MsiSetInternalUI(uint level,IntPtr owner);
        [DllImport("wintrust.dll",ExactSpelling=true)]internal static extern int WinVerifyTrust(IntPtr window,ref Guid action,ref TrustData data);
    }
}
