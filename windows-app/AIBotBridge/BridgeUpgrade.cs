using System.Diagnostics;
using System.Text.Json;

namespace AIBotBridge;

internal static class BridgeUpgrade
{
    internal static string InstallAndStart(){
        AppProfileGuard.EnsureDesktopProfile();
        if(!BridgeElevation.IsAdministrator)throw new UnauthorizedAccessException("bridge_administrator_required");
        string owner=BridgeElevation.Owner;
        using var gate=new Mutex(false,@"Local\AIBotBridge.Upgrade."+owner);
        bool acquired;
        try{acquired=gate.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
        if(!acquired)throw new IOException("bridge_upgrade_in_progress");
        try{
            string? previous=StartupRegistration.InstalledExecutable();
            if(previous is not null){
                var manifest=BridgeInstallation.Read(previous,owner)??throw new IOException("bridge_previous_installation_unverified");
                if(BridgeInstallation.MatchesSource(manifest)){StartupRegistration.StartInstalled(previous);return previous;}
            }
            bool restartPrevious=previous is not null&&StartupRegistration.IsRunning(previous);
            try{
                if(restartPrevious){
                    BridgeLifetime.RequestExit();
                    var timer=Stopwatch.StartNew();
                    while(StartupRegistration.IsRunning(previous!)){
                        if(timer.Elapsed>=TimeSpan.FromSeconds(60))throw new IOException("bridge_previous_version_did_not_exit");
                        Thread.Sleep(250);
                    }
                }
                string installed=BridgeInstallation.Configure();
                StartupRegistration.StartInstalled(installed);
                Tab5LocalDiagnostics.Write("BRIDGE_UPGRADE stage=started");
                return installed;
            }catch{
                if(previous is not null){
                    try{
                        StartupRegistration.ConfigureInstalled(previous);
                        if(restartPrevious&&!StartupRegistration.IsRunning(previous))StartupRegistration.StartInstalled(previous);
                        Tab5LocalDiagnostics.Write("BRIDGE_UPGRADE stage=rollback");
                    }catch(Exception ex){Tab5LocalDiagnostics.Write("BRIDGE_UPGRADE stage=rollback_failed error="+ex.GetType().Name);}
                }
                throw;
            }
        }finally{gate.ReleaseMutex();}
    }

    internal static void Run(string[] args){
        try{
            AppProfileGuard.EnsureDesktopProfile();
            string? image=args.Length==3?Path.GetFullPath(args[1]):null;
            string? sha=args.Length==3?args[2]:null;
            if(image is not null&&Tab5OtaPackage.Load(image).Sha256!=sha)throw new IOException("tab5_upgrade_image_mismatch");
            if(!BridgeElevation.IsAdministrator){
                // One normal desktop launch, one UAC. After consent the same
                // AI-bot executable installs and hands off to the fixed task.
                Environment.ExitCode=BridgeElevation.Start(Environment.ProcessPath!,new[]{"--elevated-tray",BridgeElevation.Owner}.Concat(args),true,true);
                return;
            }
            Tab5LocalDiagnostics.Write("BRIDGE_UPGRADE stage=authorized");
            string installed=InstallAndStart();
            if(image is not null)Tab5ResidentUpgrade.RunAsync(image,sha!).GetAwaiter().GetResult();
            Console.WriteLine(JsonSerializer.Serialize(new{installed,firmware=image is not null}));
        }catch(Exception ex){
            Tab5LocalDiagnostics.Write("BRIDGE_UPGRADE stage=failed error="+ex.GetType().Name);
            Console.Error.WriteLine("BRIDGE_UPGRADE_FAILED "+ex.GetType().Name+": "+ex.Message);
            Environment.ExitCode=1;
        }
    }
}
