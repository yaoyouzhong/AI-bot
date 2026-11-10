using System.Security.Principal;
namespace AIBotBridge;
internal static class BridgeAdminSelfTest
{
    internal static async Task ReadDesktopAsync(){
        AppProfileGuard.EnsureDesktopProfile();
        var rows=new List<object>();bool readable=false;
        // Subscription only: never navigate, focus, type or launch a CLI.
        var desktop=new Tab5CodexDesktop(openThread:_=>false);
        foreach(var task in Tab5CodexCatalog.Recent().Take(3)){
            try{using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(15));
                var state=await desktop.ReadAsync(task.Id,timeout.Token);
                rows.Add(new{available=true,runtime=state.Runtime,status=state.Status});readable=true;break;
            }catch(Exception ex) when(ex is IOException or InvalidOperationException or OperationCanceledException or UnauthorizedAccessException or System.Text.Json.JsonException){rows.Add(new{available=false,error=ex.GetType().Name});}
        }
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new{administrator=BridgeElevation.IsAdministrator,readOnly=true,readable,observations=rows}));
        Environment.ExitCode=readable?0:1;
    }
    internal static void Run(){
        int checks=0;void Check(bool ok,string reason){checks++;if(!ok)throw new InvalidOperationException(reason);}
        var owners=new Dictionary<int,ClaudeConsoleOwners.Owner>{
            [10]=new(10,20,300,"claude.exe","owner",1,8192),
            [11]=new(11,99,400,"bridge.exe","owner",1,8192),
            [20]=new(20,30,200,"powershell.exe","owner",1,8192),
            [30]=new(30,99,100,"cmd.exe","owner",1,8192)
        };
        bool Owners(params uint[] ids)=>ClaudeConsoleOwners.Matches(ids,10,11,id=>owners[id],path=>path is "powershell.exe" or "cmd.exe");
        Check(Owners(10,11)&&Owners(10,11,20)&&Owners(30,11,20,10),"direct console and verified waiting shell chain accepted");
        Check(!Owners(10,20)&&!Owners(10,11,11)&&!Owners(10,11,30),"missing worker, duplicate and non-contiguous ancestor refused");
        var shell=owners[20];
        foreach(var changed in new[]{shell with{Parent=20},shell with{Started=301},shell with{Sid="other"},shell with{Session=2},
            shell with{Integrity=12288},shell with{Image="claude.exe"},shell with{Image="unrelated.exe"},shell with{Pid=99}}){
            owners[20]=changed;Check(!Owners(10,11,20,30),"changed or unrelated console owner refused");
        }
        owners[20]=shell;
        string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        Check(ClaudeConsoleOwners.TrustedShell(Path.Combine(windows,@"System32\WindowsPowerShell\v1.0\powershell.exe"))&&
            ClaudeConsoleOwners.TrustedShell(Path.Combine(windows,@"System32\cmd.exe")),"system shell paths recognized");
        Check(!ClaudeConsoleOwners.TrustedShell(Path.Combine(Path.GetTempPath(),"powershell.exe"))&&
            !ClaudeConsoleOwners.TrustedShell(Path.Combine(windows,@"System32\unrelated.exe")),"shell name alone never grants access");

        var worker=owners[11];owners[11]=worker with{Integrity=12288};
        Check(Owners(10,11,20,30),"administrator worker accepts verified ordinary shell chain");
        owners[10]=owners[10] with{Integrity=12288};owners[20]=owners[20] with{Integrity=12288};owners[30]=owners[30] with{Integrity=12288};
        Check(Owners(10,11,20,30),"administrator worker accepts administrator shell chain");
        owners[11]=worker;Check(!Owners(10,11,20,30),"ordinary worker cannot address administrator terminal");
        foreach(var pair in new[]{(8192,8192),(12288,8192),(12288,12288)})Check(ClaudeConsoleOwners.CanAccess(pair.Item1,pair.Item2),"interactive access supported");
        foreach(var pair in new[]{(8192,12288),(12288,4096),(16384,8192),(12288,16384),(4096,4096)})Check(!ClaudeConsoleOwners.CanAccess(pair.Item1,pair.Item2),"unsupported integrity refused");
        owners[11]=worker with{Integrity=12288,Sid="different"};Check(!Owners(10,11),"administrator cannot cross users");
        owners[11]=worker with{Integrity=12288,Session=2};Check(!Owners(10,11),"administrator cannot cross desktop sessions");
        string consoleScope=Guid.NewGuid().ToString("N")+Guid.NewGuid().ToString("N");
        using(ClaudeConsoleLease.Acquire(consoleScope)){
            bool blocked=Task.Run(()=>{
                try{using var competing=ClaudeConsoleLease.Acquire(consoleScope,75);return false;}
                catch(IOException e) when(e.Message=="Native console probe busy"){return true;}
            }).GetAwaiter().GetResult();
            Check(blocked,"same-console discovery and input must never attach concurrently");
            Check(Task.Run(()=>{using var other=ClaudeConsoleLease.Acquire(new string('F',64),75);return true;}).GetAwaiter().GetResult(),"independent consoles must not share a global lock");
        }
        Check(Task.Run(()=>{using var released=ClaudeConsoleLease.Acquire(consoleScope,75);return true;}).GetAwaiter().GetResult(),"completed console operation did not release the lease");
        foreach(string path in new[]{"..\\outside.dll","a\\..\\outside.dll","C:\\outside.dll","file:stream","a//b"}){
            bool refused=false;try{BridgeInstallation.Resolve(Path.GetTempPath(),path);}catch(IOException){refused=true;}Check(refused,"payload traversal refused");
        }
        string sid="S-1-5-21-1-2-3-1001";
        using(var identity=WindowsIdentity.GetCurrent()){
            string owner=identity.User!.Value,name=identity.Name;
            Check(LegacyClaudeAdminCleanup.SameSid(name,owner)&&!LegacyClaudeAdminCleanup.SameSid(name,sid),"task principal matches owner SID");
            string localPrefix=Environment.MachineName+"\\";
            if(name.StartsWith(localPrefix,StringComparison.OrdinalIgnoreCase))Check(LegacyClaudeAdminCleanup.SameSid(name[localPrefix.Length..],owner),"short local account resolves");
        }
        string acl=$"O:BAG:SYD:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGX;;;{sid})";
        Check(LegacyClaudeAdminCleanup.TaskAclMatches(acl,sid)&&LegacyClaudeAdminCleanup.TaskAclMatches(acl.Replace("GRGX","0x1200a9"),sid),"owner can read and launch only");
        Check(!LegacyClaudeAdminCleanup.TaskAclMatches(acl.Replace("GRGX","GA"),sid)&&!LegacyClaudeAdminCleanup.TaskAclMatches(acl+"(A;;GRGX;;;WD)",sid)&&!LegacyClaudeAdminCleanup.TaskAclMatches(acl.Replace("O:BA","O:"+sid),sid),"task rewrite and other users refused");
        var files=new Dictionary<string,string>{{"AIBotBridge.dll",new string('a',64)}};
        string hash=BridgeInstallation.Identity(files);files["AIBotBridge.dll"]=new string('b',64);
        Check(hash!=BridgeInstallation.Identity(files),"changed product gets a distinct protected candidate");
        Check(BridgeInstallation.OwnedPath(Path.Combine(BridgeInstallation.Root,"v1-"+new string('A',64)+"-"+sid),sid)&&!BridgeInstallation.OwnedPath(Path.Combine(Path.GetTempPath(),"v1-"+new string('A',64)+"-"+sid),sid),"cleanup scoped to protected product and owner");
        var allow=new BridgeFirewall.Rule("old.exe",1,1,true,6,6,"*","*","*","*","All");
        var scoped=BridgeFirewall.Plan([allow,allow with{Protocol=17}],"old.exe",18765);
        Check(scoped.Length==2&&scoped.All(r=>r.Profiles==6&&r.RemoteAddresses=="LocalSubnet")&&scoped.Any(r=>r.Port==18765)&&scoped.Any(r=>r.Port==18766),"inherit approved profiles but scope both LAN ports");
        Check(BridgeFirewall.Plan([allow with{App="lookalike.exe"},allow with{Enabled=false},allow with{Direction=2},allow with{Constrained=true}],"old.exe",18765).Length==0,"ignore unrelated, disabled, outbound and constrained allows");
        Check(BridgeFirewall.Plan([allow,allow with{Action=0}],"old.exe",18765).Length==0,"do not clone permissions around an explicit block");
        Check(BridgeFirewall.Plan([allow with{LocalPorts="18000-18500",Profiles=2}],"old.exe",18765).Length==0,"do not authorize a port outside the existing grant");
        Check(BridgeFirewall.Plan([allow with{LocalPorts="18760-18769",Profiles=2,LocalAddresses="192.168.0.5",RemoteAddresses="192.168.0.9",InterfaceTypes="Wireless"}],"old.exe",18765)
            .Single()==new BridgeFirewall.Grant(6,18765,2,"192.168.0.5","192.168.0.9","*","Wireless"),"retain narrower source restrictions");
        Console.WriteLine($"BRIDGE_ADMIN_SELF_TEST_OK {checks} checks; no installation, elevation or input");
    }
}
