namespace AIBotBridge;

internal static class AppProfileGuardSelfTest
{
    internal static void Run() {
        static void Check(bool value,string label) { if(!value)throw new Exception("Profile guard: "+label); }
        Check(AppProfileGuard.IsPackageData(@"\\?\C:\Users\test\AppData\Local\Packages\Launcher_id\LocalCache\Roaming\AI-bot\settings.json"),"virtualized physical file rejected");
        Check(AppProfileGuard.IsPackageData("C:/Users/test/AppData/Local/Packages/Launcher_id/RoamingState/AI-bot"),"redirected known-folder path rejected");
        Check(!AppProfileGuard.IsPackageData(@"C:\Users\test\AppData\Roaming\AI-bot"),"ordinary profile accepted");
        Check(!AppProfileGuard.IsPackageData(@"\\server\profiles\test\Roaming\AI-bot"),"enterprise roaming profile accepted");
        Check(!AppProfileGuard.IsPackageData(@"C:\work\Packages\project\settings.json"),"ordinary package source directory accepted");
        string folder=Path.Combine(Path.GetTempPath(),"aibot-profile-guard-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try {
            string file=Path.Combine(folder,"probe.txt");File.WriteAllText(file,"unchanged");
            Check(AppProfileGuard.PhysicalPath(folder) is {Length:>0},"directory handle resolves");
            Check(AppProfileGuard.PhysicalPath(file) is {Length:>0},"file handle resolves");
            Check(AppProfileGuard.PhysicalPath(Path.Combine(folder,"missing")) is null,"fresh-profile missing file accepted without creation");
            Check(File.ReadAllText(file)=="unchanged"&&Directory.GetFiles(folder).Length==1,"probe is read-only");
        } finally { Directory.Delete(folder,true); }
        // This tests the real process launch environment without accessing content.
        try {AppProfileGuard.Validate();Console.WriteLine("PROFILE_ENVIRONMENT=desktop");}
        catch(IOException ex) when(ex.Message==AppProfileGuard.Recovery) {Console.WriteLine("PROFILE_ENVIRONMENT=redirected; normal startup blocked");}
        AppPaths.BeginPublicSelfTest();
        Check(AppPaths.GetFolderPath(Environment.SpecialFolder.ApplicationData).Contains("public-profile-",StringComparison.Ordinal),"explicit isolated tests remain independent of launcher");
        Console.WriteLine("PROFILE_GUARD_OK physical handles, redirection, fresh profile and isolated tests");
    }
}
