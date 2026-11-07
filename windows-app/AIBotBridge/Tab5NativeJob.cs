using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace AIBotBridge;

// The desktop tray owns profile access. IPC callers can inspect or trigger one
// previously armed suite; they cannot supply an executable, path or prompt.
internal static class Tab5NativeJob
{
    private sealed record Plan(Guid Id,string Host,Guid Session,string Scope,string Binary,DateTimeOffset Expires);
    private static string Root=>Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"AI-bot","native-acceptance");
    private static string Binary(){
        using var input=File.OpenRead(typeof(Tab5NativeJob).Assembly.Location);
        return Convert.ToHexString(SHA256.HashData(input));
    }
    private static string Executable()=>Path.ChangeExtension(typeof(Tab5NativeJob).Assembly.Location,".exe");
    private static Plan? Active(){
        string file=Path.Combine(Root,"armed.json");
        if(!File.Exists(file))return null;
        if(new FileInfo(file).Length>4096)throw new IOException("invalid_plan");
        var plan=JsonSerializer.Deserialize<Plan>(File.ReadAllBytes(file))??throw new IOException("invalid_plan");
        if(plan.Id==Guid.Empty||plan.Session==Guid.Empty||plan.Host is not ("terminal" or "vscode")||
            plan.Scope is not {Length:64}||!plan.Scope.All(Uri.IsHexDigit)||plan.Binary is not {Length:64}||!plan.Binary.All(Uri.IsHexDigit))throw new IOException("invalid_plan_identity");
        return plan;
    }
    private static FileStream LockPlan(){
        Directory.CreateDirectory(Root);
        return new FileStream(Path.Combine(Root,"plan.lock"),FileMode.OpenOrCreate,FileAccess.Write,FileShare.None);
    }
    private static string Used(string host,Guid session)=>Path.Combine(Root,$"{host}-{session:N}.used");
    private static string DirectoryFor(Plan plan)=>Path.Combine(Root,plan.Id.ToString("N"));
    internal static JsonElement Inspect(string host,Guid session){
        try{
            object? view=host switch{
                "terminal"=>ClaudeNativeTerminal.Find(session)?.Inspect(),
                "vscode"=>ClaudeNativeCode.Find(session,explain:true)?.Inspect(),
                _=>throw new ArgumentException("unsupported_host")
            };
            return JsonSerializer.SerializeToElement(view??new{available=false,host,error="original_session_unavailable"});
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or System.Runtime.InteropServices.COMException or System.Windows.Automation.ElementNotAvailableException){
            string reason=e is IOException&&e.Message.Length<=80&&e.Message.StartsWith("native_")&&e.Message.All(c=>char.IsAsciiLetter(c)||c=='_')?e.Message:e.GetType().Name;
            return JsonSerializer.SerializeToElement(new{available=false,host,error=reason});
        }
    }
    // Explicit local arming in a normal desktop context, never an IPC operation.
    internal static object Arm(string host,Guid session){
        AppProfileGuard.EnsureDesktopProfile();
        using var gate=LockPlan();
        string? scope=host switch{
            "terminal"=>ClaudeNativeTerminal.Find(session)?.Scope(),
            "vscode"=>ClaudeNativeCode.Find(session)?.Scope(),
            _=>throw new ArgumentException("unsupported_host")
        };
        if(scope==null)throw new IOException("original_session_unavailable");
        if(File.Exists(Used(host,session)))throw new IOException("original_session_already_attempted");
        var previous=Active();
        if(previous!=null&&File.Exists(Path.Combine(DirectoryFor(previous),"reserved"))&&
            !File.Exists(Path.Combine(DirectoryFor(previous),"finished.json")))throw new IOException("previous_batch_unresolved");
        var plan=new Plan(Guid.NewGuid(),host,session,scope,Binary(),DateTimeOffset.UtcNow.AddHours(8));
        Directory.CreateDirectory(DirectoryFor(plan));
        string tmp=Path.Combine(DirectoryFor(plan),"armed.tmp");
        File.WriteAllBytes(tmp,JsonSerializer.SerializeToUtf8Bytes(plan));
        File.Move(tmp,Path.Combine(Root,"armed.json"),true);
        return new{plan.Id,plan.Host,plan.Expires,plan.Binary};
    }
    internal static object Status(){
        var plan=Active();
        if(plan==null)return new{armed=false};
        string dir=DirectoryFor(plan),finished=Path.Combine(dir,"finished.json");
        return new{armed=true,plan.Id,plan.Host,plan.Expires,currentBuild=plan.Binary==Binary(),reserved=File.Exists(Path.Combine(dir,"reserved")),
            finished=File.Exists(finished),outcome=File.Exists(finished)?JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(finished)):(JsonElement?)null};
    }
    internal static object Start(Tab5Service service){
        using var gate=LockPlan();
        var plan=Active()??throw new IOException("no_armed_plan");
        if(plan.Binary!=Binary())throw new IOException("armed_build_changed");
        if(plan.Expires<=DateTimeOffset.UtcNow)throw new IOException("armed_plan_expired");
        string directory=DirectoryFor(plan),reserved=Path.Combine(directory,"reserved");
        if(File.Exists(reserved))return Status(); // polling or duplicate caller
        if(File.Exists(Used(plan.Host,plan.Session)))throw new IOException("original_session_already_attempted");
        var view=Inspect(plan.Host,plan.Session);
        if(!view.GetProperty("available").GetBoolean()||view.GetProperty("scope").GetString()!=plan.Scope)
            throw new IOException("original_scope_unavailable");
        if(view.GetProperty("state").GetString()!="idle"||!view.GetProperty("draftEmpty").GetBoolean())throw new IOException("original_composer_busy_or_nonempty");
        if(service.Busy||service.ScreenPreviewContext is not {Available:true} context||!context.Transports.Contains("USB"))throw new IOException("paired_USB_busy_or_unavailable");
        using var parent=Process.GetCurrentProcess();
        if(!SameExecutable(parent)||!File.Exists(Executable()))throw new IOException("resident_apphost_required");
        var stamp=parent.StartTime.ToUniversalTime().ToFileTimeUtc();
        using(var mark=new FileStream(reserved,FileMode.CreateNew,FileAccess.Write,FileShare.None)){
            mark.Write(JsonSerializer.SerializeToUtf8Bytes(new{parent=parent.Id,stamp}));mark.Flush(true);
        }
        try{
            var start=HiddenApp();
            start.ArgumentList.Add("--tab5-armed-worker");start.ArgumentList.Add(plan.Id.ToString());
            using var child=Process.Start(start)??throw new IOException("worker_start_failed");
            return new{armed=true,started=true,id=plan.Id};
        }catch{
            File.WriteAllText(Path.Combine(directory,"finished.json"),"{\"passed\":false,\"stage\":\"worker_start_failed\"}");
            throw;
        }
    }
    private static bool SameExecutable(Process p)=>string.Equals(p.MainModule?.FileName,Executable(),StringComparison.OrdinalIgnoreCase);
    private static ProcessStartInfo HiddenApp()=>new(Executable()){UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,WorkingDirectory=Path.GetDirectoryName(Executable())!};
    internal static int Worker(Guid id){
        AppProfileGuard.EnsureDesktopProfile();
        var plan=Active()??throw new IOException("no_armed_plan");
        if(plan.Id!=id||plan.Expires<=DateTimeOffset.UtcNow||plan.Binary!=Binary())throw new IOException("plan_changed");
        string dir=DirectoryFor(plan),run=Path.Combine(dir,"hardware");
        using var ticket=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(dir,"reserved")));
        using var parent=Process.GetProcessById(ticket.RootElement.GetProperty("parent").GetInt32());
        if(!SameExecutable(parent)||parent.StartTime.ToUniversalTime().ToFileTimeUtc()!=ticket.RootElement.GetProperty("stamp").GetInt64())throw new IOException("resident_identity_changed");
        using(var once=new FileStream(Path.Combine(dir,"worker.started"),FileMode.CreateNew,FileAccess.Write,FileShare.None))once.Flush(true);
        bool askedExit=false,restored=false;int code=1;string phase="preflight";
        try{
            if(Tab5NativeAcceptance.Run(Path.Combine(dir,"probe"),plan.Host,plan.Scope,true)!=0)return 1;
            phase="resident_exit";askedExit=true;BridgeLifetime.RequestExit();
            if(!parent.WaitForExit(25000))throw new IOException("resident_did_not_exit");
            using(var once=new FileStream(Used(plan.Host,plan.Session),FileMode.CreateNew,FileAccess.Write,FileShare.None)){
                once.Write(plan.Id.ToByteArray());
                once.Flush(true);
            }
            phase="physical_device";code=Tab5NativeAcceptance.Run(run,plan.Host,plan.Scope,false);return code;
        }finally{
            try{
                if(askedExit&&parent.WaitForExit(5000)&&Binary()==plan.Binary){
                    using var resident=Process.Start(HiddenApp());restored=resident!=null&&!resident.WaitForExit(1500);
                }
            }finally{
                string file=phase=="preflight"?Path.Combine(dir,"probe.json"):run+".json";
                JsonElement? report=File.Exists(file)?JsonSerializer.Deserialize<JsonElement>(File.ReadAllBytes(file)):null;
                string tmp=Path.Combine(dir,"finished.tmp");
                File.WriteAllBytes(tmp,JsonSerializer.SerializeToUtf8Bytes(new{passed=code==0&&restored,phase,
                    resident=!askedExit?"not_stopped":restored?"restored":"restore_unconfirmed",
                    cases=report?.GetProperty("cases"),reason=report?.GetProperty("reason"),reportPath=file}));
                File.Move(tmp,Path.Combine(dir,"finished.json"));
            }
        }
    }
}
