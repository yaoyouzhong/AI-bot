using Microsoft.Win32;
using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

internal static class TightVncSelfTest
{
    private static int checks;
    private static void Require(bool condition,string name){checks++;if(!condition)throw new InvalidOperationException(name);}
    internal sealed class FakeController(WifiKeyboardAccessStore store,TightVncState state) : ITightVncController
    {
        internal TightVncState State=state;
        internal int Changes;
        internal TightVncResult? Result;
        internal TaskCompletionSource<TightVncResult>? Pending;
        public Task<TightVncState> InspectAsync(CancellationToken token)=>Task.FromResult(State);
        public async Task<TightVncResult> ChangeAsync(string action,string instance,CancellationToken token) {
            Changes++;
            var result=Pending is not null?await Pending.Task:Result??new(true,"完成。");
            if(result.Ok&&action=="install"){store.Save(true,"vncTest9"u8);State=new(TightVncKind.Managed,true,true,new string('1',32));}
            if(result.Ok&&action is "authorize" or "start")store.Save(true,"vncTest9"u8);
            if(result.Ok&&action=="uninstall"){store.Delete();State=new(TightVncKind.Missing);}
            return result;
        }
    }
    private static IEnumerable<Control> Desc(Control c){foreach(Control child in c.Controls){yield return child;foreach(var nested in Desc(child))yield return nested;}}
    private static void Until(Func<bool> condition) {
        var stop=DateTime.UtcNow.AddSeconds(4);
        while(!condition()&&DateTime.UtcNow<stop){Application.DoEvents();Thread.Sleep(10);}
        Require(condition(),"UI action did not settle");
    }
    internal static void Run(string directory) {
        Directory.CreateDirectory(directory);
        var states=new[]{new TightVncState(TightVncKind.Missing),new(TightVncKind.Managed,true,true,new string('1',32)),new(TightVncKind.Managed,false,true,new string('1',32)),new(TightVncKind.Existing,true,true),new(TightVncKind.Existing,false,true),new(TightVncKind.Existing,true,false,Detail:TightVncPackage.Requirements),new(TightVncKind.Conflict,Detail:"组件在安装后已被修改或升级，请通过 Windows 设置管理现有安装。"),new(TightVncKind.RestartRequired,Detail:"卸载等待重启完成。")};
        states=[..states,new(TightVncKind.Existing,false,false,NeedsAuthorization:true),new(TightVncKind.CleanupRequired,Instance:new string('1',32))];
        int index=0;var measurements=new List<object>();
        foreach(var state in states)foreach(float scale in new[]{1f,1.5f,2f}) {
            var store=new WifiKeyboardAccessStore(Path.Combine(directory,$"ui-{index++}.dat"));
            var backend=new FakeController(store,state);
            using var form=new WifiUnlockForm(store,()=>true,_=>Task.CompletedTask,backend);
            form.Show();Application.DoEvents();form.Scale(new SizeF(scale,scale));Application.DoEvents();
            measurements.Add(new{kind=state.Kind.ToString(),state.Running,state.Compatible,geometryScale=scale,deviceDpi=form.DeviceDpi,fontPoints=form.Font.Size,clientWidth=form.ClientSize.Width,clientHeight=form.ClientSize.Height});
            var controls=Desc(form).ToArray();var remove=controls.Single(c=>c.Name=="wifi-component-remove");
            Require(remove.Visible==state.CanRemove,"Removal exposed for unowned component");
            Require(!controls.Any(c=>c is TextBox),"Component page requires a manually entered password");
            var footer=controls.Where(c=>c.Name is "wifi-save" or "wifi-check").ToArray();
            Require(footer[0].Top==footer[1].Top&&footer[0].Height==footer[1].Height,"Footer buttons not aligned");
            foreach(var control in controls.Where(c=>c.Visible&&c.Name is "wifi-save" or "wifi-check" or "wifi-component-action" or "wifi-component-remove"))
                Require(form.ClientRectangle.Contains(form.RectangleToClient(control.RectangleToScreen(control.ClientRectangle))),"Clipped component action");
            Require(backend.Changes==0,"Opening settings modified a component");
            using var bitmap=new Bitmap(form.Width,form.Height);form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(directory,$"{state.Kind}-{state.Running}-{state.Compatible}-{scale:0.0}.png"));
            form.Close();
        }
        File.WriteAllText(Path.Combine(directory,"ui-measurements.json"),JsonSerializer.Serialize(measurements));
        var access=new WifiKeyboardAccessStore(Path.Combine(directory,"access.dat"));
        access.Save(false,"vncTest9"u8);byte[] before=access.EncryptedSnapshot();access.Save(true,"NextKey8"u8);access.RestoreEncrypted(before,true);
        var restored=access.Read();Require(!restored.Enabled&&restored.Password.AsSpan().SequenceEqual("vncTest9"u8),"Failed install lost previous access record");CryptographicOperations.ZeroMemory(restored.Password);
        access.Delete();Require(!access.Exists,"Component credential deletion failed");
        AutomaticExistingUiTest(access,directory);
        access.Delete();
        ControllerTestsAsync(access).GetAwaiter().GetResult();
        for(int i=0;i<1000;i++){byte[] value=TightVncPackage.NewSecret();Require(WifiKeyboardAccessStore.Valid(value),"Generated secret is incompatible");CryptographicOperations.ZeroMemory(value);}
        Require(TightVncPackage.Successful(0)&&TightVncPackage.Successful(3010)&&!TightVncPackage.Successful(1603)&&!TightVncPackage.Successful(1223)&&!TightVncPackage.Successful(1641),"MSI failure/reboot handling incorrect");
        var externalOptions=TightVncPackage.Options.ToDictionary(p=>p.Key,p=>p.Value);
        externalOptions["RfbPort"]=5901;externalOptions["UseMirrorDriver"]=1;
        Require(TightVncMachine.Compatible(name=>externalOptions[name]),"Secure existing custom port was rejected");
        foreach(var option in new[]{("RfbPort",80),("LoopbackOnly",0),("UseVncAuthentication",0),("AcceptHttpConnections",1)}) {
            int previousOption=externalOptions[option.Item1];externalOptions[option.Item1]=option.Item2;
            Require(!TightVncMachine.Compatible(name=>externalOptions[name]),"Unsafe existing configuration accepted");externalOptions[option.Item1]=previousOption;
        }
        foreach(bool cancel in new[]{true,false}) {
            var backend=new FakeController(access,new(TightVncKind.Missing)){Result=cancel?new(false,"已取消管理员授权。"):null};
            using var form=new WifiUnlockForm(access,()=>true,_=>Task.CompletedTask,backend);form.Show();Application.DoEvents();
            var controls=Desc(form).ToArray();((Button)controls.Single(c=>c.Name=="wifi-component-action")).PerformClick();
            Until(()=>controls.Single(c=>c.Name=="wifi-save").Enabled);
            Require(backend.Changes==1&&access.Enabled!=cancel,"Install/cancel result not reflected in settings");
            if(!cancel) {
                form.ConfirmRemoval=()=>false;((Button)controls.Single(c=>c.Name=="wifi-component-remove")).PerformClick();Require(backend.Changes==1,"Removal ignored user cancellation");
                form.ConfirmRemoval=()=>true;((Button)controls.Single(c=>c.Name=="wifi-component-remove")).PerformClick();Until(()=>controls.Single(c=>c.Name=="wifi-save").Enabled);
                Require(backend.Changes==2&&!access.Exists,"Owned removal failed to clear only component access");
            }
            form.Close();
        }
        var waiting=new FakeController(access,new(TightVncKind.Missing)){Pending=new(TaskCreationOptions.RunContinuationsAsynchronously)};
        using(var form=new WifiUnlockForm(access,()=>true,_=>Task.CompletedTask,waiting)) {
            form.Show();Application.DoEvents();var controls=Desc(form).ToArray();var install=(Button)controls.Single(c=>c.Name=="wifi-component-action");install.PerformClick();
            Require(!install.Enabled&&!controls.Single(c=>c.Name=="wifi-save").Enabled,"Concurrent setup was not gated");
            form.Close();Require(!form.IsDisposed,"Form closed while privileged operation was pending");
            waiting.Pending.SetResult(new(false,"已取消管理员授权。"));Until(()=>install.Enabled);Require(waiting.Changes==1,"Cancelled setup retried itself");form.Close();
        }
        PacketTestsAsync().GetAwaiter().GetResult();
        File.WriteAllText(Path.Combine(directory,"result.json"),JsonSerializer.Serialize(new{passed=true,checks,realInstallPerformed=false,realInputSent=false}));
        Console.WriteLine($"TIGHTVNC_COMPONENT_SELF_TEST_OK checks={checks}; {states.Length} states, three geometry scales, owned removal, UAC cancel, pending close, encrypted rollback, bounded IPC; no system changes");
    }
    private static void AutomaticExistingUiTest(WifiKeyboardAccessStore access,string directory) {
        access.Delete();
        using var form=new WifiUnlockForm(access,()=>true,_=>Task.CompletedTask,new FakeController(access,new(TightVncKind.Existing,true,true)));
        form.Show();Application.DoEvents();var controls=Desc(form).ToArray();
        var connect=(Button)controls.Single(c=>c.Name=="wifi-component-action");
        Require(connect.Visible&&connect.Text=="自动接入"&&!controls.Any(c=>c is TextBox),"Existing installation still requires manual credentials");
        connect.PerformClick();Until(()=>controls.Single(c=>c.Name=="wifi-save").Enabled);
        Require(access.Enabled&&connect.Text=="重新接入","Automatic connection did not enable local access");
        using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,form.Size));bitmap.Save(Path.Combine(directory,"Existing-saved.png"));}
        form.Close();
    }
    private static async Task ControllerTestsAsync(WifiKeyboardAccessStore access) {
        access.Save(true,"vncTest9"u8);byte[] previous=access.EncryptedSnapshot();
        var controller=new TightVncController(access){CanEdit=()=>true,Check=_=>Task.CompletedTask};
        controller.Elevate=async(_,_,_,commit,_)=>{await commit(false);return new(false,"synthetic failed commit");};
        var rollback=await controller.ChangeAsync("install","",CancellationToken.None);
        Require(!rollback.Ok&&access.EncryptedSnapshot().AsSpan().SequenceEqual(previous),"Failed installation did not restore encrypted access record");
        controller.Elevate=(_,_,_,_,_)=>Task.FromResult(new TightVncResult(false,"synthetic uncertain commit",KeepCredentials:true));
        var uncertain=await controller.ChangeAsync("install","",CancellationToken.None);
        Require(!uncertain.Ok&&access.Exists&&!access.Enabled&&!access.EncryptedSnapshot().AsSpan().SequenceEqual(previous),"Uncertain committed key was lost or enabled: "+uncertain.Diagnostic);
        access.RestoreEncrypted(previous,true);
        controller.Elevate=(_,_,_,_,_)=>throw new System.ComponentModel.Win32Exception(1223);
        var cancelled=await controller.ChangeAsync("uninstall","",CancellationToken.None);
        Require(!cancelled.Ok&&access.Enabled&&access.EncryptedSnapshot().AsSpan().SequenceEqual(previous),"UAC cancellation disabled existing Wi-Fi access");
        controller.Elevate=(_,_,_,_,_)=>Task.FromResult(new TightVncResult(false,"synthetic incomplete removal"));
        var failed=await controller.ChangeAsync("uninstall","",CancellationToken.None);
        Require(!failed.Ok&&access.Exists&&!access.Enabled,"Incomplete removal did not retain a disabled key");
        controller.Elevate=(_,_,_,_,_)=>Task.FromResult(new TightVncResult(true,"synthetic removal"));
        var removed=await controller.ChangeAsync("uninstall","",CancellationToken.None);
        Require(removed.Ok&&!access.Exists,"Successful component removal retained the key");
        controller.ReadAccess=()=>"only123!"u8.ToArray();
        var connected=await controller.ChangeAsync("authorize","",CancellationToken.None);
        Require(connected.Ok&&access.Enabled,"Existing password was not imported automatically");
        controller.Check=_=>throw new IOException("synthetic failed existing authentication");
        var oldImport=access.EncryptedSnapshot();var rejected=await controller.ChangeAsync("authorize","",CancellationToken.None);
        Require(!rejected.Ok&&access.EncryptedSnapshot().AsSpan().SequenceEqual(oldImport),"Failed existing authentication overwrote local access");
        CryptographicOperations.ZeroMemory(oldImport);access.Delete();
        CryptographicOperations.ZeroMemory(previous);
    }
    private static async Task PacketTestsAsync() {
        using var stream=new MemoryStream();await TightVncElevation.WriteAsync(stream,new {ok=true},CancellationToken.None);stream.Position=0;
        var value=await TightVncElevation.ReadAsync<JsonElement>(stream,CancellationToken.None);Require(value.GetProperty("ok").GetBoolean(),"IPC framing roundtrip failed");
        foreach(int length in new[]{-1,0,1,8193,int.MaxValue}) {
            byte[] bytes=new byte[4];BinaryPrimitives.WriteInt32LittleEndian(bytes,length);using var invalid=new MemoryStream(bytes);
            bool rejected=false;try{await TightVncElevation.ReadAsync<JsonElement>(invalid,CancellationToken.None);}catch(IOException){rejected=true;}Require(rejected,"Oversized IPC accepted");
        }
    }
    internal static async Task SandboxAsync(string directory,bool testRollback=true,bool keepInstalled=false) {
        if(Environment.UserName!="WDAGUtilityAccount"||!Directory.Exists(@"C:\Users\WDAGUtilityAccount"))throw new InvalidOperationException("Real installation test is restricted to Windows Sandbox.");
        if(TightVncMachine.Inspect(TightVncPackage.Sid).Kind!=TightVncKind.Missing)throw new InvalidOperationException("Sandbox must start without TightVNC.");
        Directory.CreateDirectory(directory);var access=keepInstalled?new WifiKeyboardAccessStore():new WifiKeyboardAccessStore(Path.Combine(directory,"synthetic-component-access.dat"));
        string sentinel=Path.Combine(directory,"synthetic-windows-pin-sentinel.txt");File.WriteAllText(sentinel,"unchanged synthetic fixture");
        var controller=new TightVncController(access){CanEdit=()=>true};bool injectedCheck=false;
        void Trace(string phase,object value)=>File.WriteAllText(Path.Combine(directory,phase+".json"),JsonSerializer.Serialize(value));
        if(testRollback) {
            controller.Check=_=>{injectedCheck=true;throw new IOException("Synthetic post-install check failure");};
            var rollback=await controller.ChangeAsync("install","",CancellationToken.None);
            Trace("rollback",new{result=rollback,injectedCheck,state=TightVncMachine.Inspect(TightVncPackage.Sid)});
            Require(injectedCheck&&!rollback.Ok&&!access.Exists&&TightVncMachine.Inspect(TightVncPackage.Sid).Kind==TightVncKind.Missing,"Real failed install did not roll back");
        }
        controller.Check=async ct=> {
            var stored=access.Read();
            try{using var tcp=new TcpClient(AddressFamily.InterNetwork){NoDelay=true};await tcp.ConnectAsync(IPAddress.Loopback,5900,ct);
                if(!WifiKeyboardSettings.TrustedPeer(tcp,out string reason)){File.WriteAllText(Path.Combine(directory,"peer-diagnostic.txt"),reason);throw new IOException("Installed peer rejected: "+reason);}
                using var rfb=new WifiKeyboardRfb(tcp.GetStream());await rfb.HandshakeAsync(stored.Password,ct,initialize:false);}
            finally{CryptographicOperations.ZeroMemory(stored.Password);}
        };
        var install=await controller.ChangeAsync("install","",CancellationToken.None);
        var ready=TightVncMachine.Inspect(TightVncPackage.Sid);
        Trace("install",new{result=install,state=ready,enabled=access.Enabled});
        Require(install.Ok&&ready.Kind==TightVncKind.Managed&&ready.Running&&access.Enabled,"Real signed MSI install/configuration/authentication failed");
        byte[] imported=TightVncMachine.ReadAccess(),installedAccess=access.Read().Password;
        try{Require(imported.AsSpan().SequenceEqual(installedAccess),"Official stored credential did not import without manual input");}
        finally{CryptographicOperations.ZeroMemory(imported);CryptographicOperations.ZeroMemory(installedAccess);}
        var reconnected=await controller.ChangeAsync("authorize",ready.Instance,CancellationToken.None);
        Require(reconnected.Ok&&TightVncMachine.Inspect(TightVncPackage.Sid).Instance==ready.Instance,"Automatic connection changed component ownership or configuration");
        if(keepInstalled) {
            // Only the guarded disposable Sandbox retains a fixture for manual
            // uninstall-wizard choices. This branch never accepts a host.
            string profile=Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData),"AI-bot");
            var pair=new Tab5Pairing("001122aabbcc",@"USB\SYNTHETIC",Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
            new Tab5PinStore().Save(pair,"4826"u8);
            File.WriteAllText(Path.Combine(profile,"sandbox-pairing-sentinel.txt"),"synthetic pairing sentinel");
            Trace("provisioned",new{ok=true,defaultProfile=true,state=ready,inputSent=false,
                pinSha256=TightVncPackage.Hash(Path.Combine(profile,"tab5-unlock-pin.dat")),
                pairingSentinelSha256=TightVncPackage.Hash(Path.Combine(profile,"sandbox-pairing-sentinel.txt"))});
            Console.WriteLine("TIGHTVNC_SANDBOX_PROVISIONED authenticated fixture retained for manual uninstall; no input");return;
        }
        var duplicate=await controller.ChangeAsync("install","",CancellationToken.None);Require(!duplicate.Ok,"Existing installation was overwritten");
        using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))using(var config=root.OpenSubKey(@"SOFTWARE\TightVNC\Server",true)!) {
            try {
                config.SetValue("LoopbackOnly",0);
                Require(TightVncMachine.Inspect(TightVncPackage.Sid).Kind==TightVncKind.Conflict,"Changed configuration still claimed as managed");
                var removeChanged=await controller.ChangeAsync("uninstall",ready.Instance,CancellationToken.None);Require(!removeChanged.Ok,"Changed service was removed");
            }finally{config.SetValue("LoopbackOnly",1);}
        }
        string otherSid="S-1-5-32-546";
        Require(TightVncMachine.Inspect(otherSid).Kind==TightVncKind.Existing,"Another user's install was adopted");
        var foreign=await TightVncMachine.ChangeAsync("uninstall",otherSid,ready.Instance,[],_=>Task.FromResult(true));Require(!foreign.Ok,"Another user's component was removed");
        var remove=await controller.ChangeAsync("uninstall",ready.Instance,CancellationToken.None);
        Require(remove.Ok&&TightVncMachine.Inspect(TightVncPackage.Sid).Kind==TightVncKind.Missing&&!access.Exists,"Owned uninstall/credential cleanup failed");
        Require(File.ReadAllText(sentinel)=="unchanged synthetic fixture","Uninstall altered unrelated PIN fixture");
        File.WriteAllText(Path.Combine(directory,"sandbox-result.json"),JsonSerializer.Serialize(new{passed=true,checks,realSignedMsiInstalled=true,realPostInstallAuthentication=true,realRollback=testRollback,realUninstall=true,modifiedAndForeignPreserved=true,inputSent=false,desktopInitializationPerformed=false}));
        Console.WriteLine("TIGHTVNC_SANDBOX_INSTALL_OK official MSI + actual worker IPC + DPAPI + authentication without desktop/input + "+(testRollback?"rollback + ":"")+"foreign/drift rejection + uninstall");
    }
    internal static async Task CleanSandboxAsync(string directory) {
        if(Environment.UserName!="WDAGUtilityAccount"||!Directory.Exists(@"C:\Users\WDAGUtilityAccount"))throw new InvalidOperationException("Cleanup is restricted to Windows Sandbox.");
        var access=new WifiKeyboardAccessStore(Path.Combine(directory,"synthetic-diagnostic-access.dat"));var state=TightVncMachine.Inspect(TightVncPackage.Sid);
        if(!access.Exists||!state.CanRemove)throw new IOException("No unchanged owned diagnostic installation; preserved");
        var result=await new TightVncController(access){CanEdit=()=>true}.ChangeAsync("uninstall",state.Instance,CancellationToken.None);
        File.WriteAllText(Path.Combine(directory,"diagnostic-cleanup.json"),JsonSerializer.Serialize(result));if(!result.Ok)throw new IOException("Diagnostic cleanup incomplete");
    }
    internal static async Task CheckDesktopAsync() {
        byte[] imported=[],saved=[];
        try {
            imported=TightVncMachine.ReadAccess();saved=new WifiKeyboardAccessStore().Read().Password;
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
            using var client=await WifiKeyboardRfb.ConnectAsync(timeout.Token);
            Console.WriteLine(JsonSerializer.Serialize(new{ok=true,automaticImportMatchesSaved=CryptographicOperations.FixedTimeEquals(imported,saved),fullSessionInitialized=true,inputSent=false,settingsChanged=false}));
        }catch(Exception ex){Console.WriteLine(JsonSerializer.Serialize(new{ok=false,error=ex.GetType().Name,code=ex.Message.StartsWith("keyboard_",StringComparison.Ordinal)?ex.Message:"",inputSent=false,settingsChanged=false}));Environment.ExitCode=1;}
        finally{CryptographicOperations.ZeroMemory(imported);CryptographicOperations.ZeroMemory(saved);}
    }
    internal static async Task DiagnoseSandboxAsync(string directory) {
        if(Environment.UserName!="WDAGUtilityAccount"||!Directory.Exists(@"C:\Users\WDAGUtilityAccount"))throw new InvalidOperationException("Diagnostic is restricted to Windows Sandbox.");
        Directory.CreateDirectory(directory);var access=new WifiKeyboardAccessStore(Path.Combine(directory,"synthetic-diagnostic-access.dat"));
        void Trace(string phase,object value)=>File.WriteAllText(Path.Combine(directory,phase+".json"),JsonSerializer.Serialize(value));
        if(TightVncMachine.Inspect(TightVncPackage.Sid).Kind==TightVncKind.Missing) {
            var setup=new TightVncController(access){CanEdit=()=>true,Check=_=>Task.CompletedTask};
            var installed=await setup.ChangeAsync("install","",CancellationToken.None);Trace("setup-without-auth-check",installed);
            if(!installed.Ok)throw new IOException("Sandbox diagnostic setup failed");
        }
        if(!access.Exists)throw new IOException("No synthetic diagnostic credential; existing install preserved");
        var stored=access.Read();
        try {
            using var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);using var config=root.OpenSubKey(@"SOFTWARE\TightVNC\Server");
            byte[] decoded=TightVncMachine.DecodeAccess((byte[])config!.GetValue("Password")!);
            try{Trace("stored-password-match",new{matches=decoded.AsSpan().SequenceEqual(stored.Password)});}finally{CryptographicOperations.ZeroMemory(decoded);}
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var tcp=new TcpClient(AddressFamily.InterNetwork){NoDelay=true};await tcp.ConnectAsync(IPAddress.Loopback,5900,timeout.Token);
            bool trusted=WifiKeyboardSettings.TrustedPeer(tcp,out string reason);Trace("peer",new{trusted,reason});if(!trusted)throw new IOException("keyboard_peer_untrusted");
            using var rfb=new WifiKeyboardRfb(tcp.GetStream());await rfb.HandshakeAsync(stored.Password,timeout.Token,phase=>File.WriteAllText(Path.Combine(directory,"auth-phase.txt"),phase));
            Trace("authentication",new{passed=true,inputSent=false});
        }catch(Exception ex){Trace("authentication-error",new{type=ex.GetType().Name,code=ex.Message.StartsWith("keyboard_",StringComparison.Ordinal)?ex.Message:"",hresult=ex.HResult});throw;}
        finally{CryptographicOperations.ZeroMemory(stored.Password);}
    }
}
