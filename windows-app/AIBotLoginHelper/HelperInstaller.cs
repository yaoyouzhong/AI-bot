using AIBot.LoginMethod;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;

internal static class HelperInstaller
{
    internal static int Run(bool install, int caller)
    {
        using var identity=WindowsIdentity.GetCurrent();
        if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) return 77;
        try {
            using var parent=Process.GetProcessById(caller);
            if (parent.SessionId <= 0 || Native.WTSGetActiveConsoleSessionId()!=(uint)parent.SessionId || Native.Locked(parent.SessionId)!=false) return 77;
            Native.Check(Native.OpenProcessToken(parent.Handle,8,out nint token));
            string owner;
            try { using var user=new WindowsIdentity(token); owner=user.User?.Value??throw new IOException("Owner missing"); }
            finally { Native.CloseHandle(token); }
            dynamic service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service")!)!;
            service.Connect(); dynamic folder=service.GetFolder("\\");
            if (install) Install(folder,service,owner); else Remove(folder,owner);
            return 0;
        } catch { return 1; }
    }

    private static void SetOwner(FileInfo file)
    {
        var acl=file.GetAccessControl();acl.SetOwner(new SecurityIdentifier("S-1-5-32-544"));file.SetAccessControl(acl);
    }

    private static void Install(dynamic folder,dynamic service,string owner)
    {
        if (Directory.Exists(HelperContract.InstallPath)) throw new IOException("Existing helper retained");
        try { dynamic existing=folder.GetTask(HelperContract.TaskName); throw new IOException("Existing task retained"); }
        catch(COMException ex) when (ex.HResult==unchecked((int)0x80070002)) { }
        catch(FileNotFoundException) { }
        var source=new DirectoryInfo(AppContext.BaseDirectory);
        var files=new Dictionary<string,string>();
        foreach(string name in HelperContract.Files) {
            var file=new FileInfo(Path.Combine(source.FullName,name));
            if(!file.Exists||file.Attributes.HasFlag(FileAttributes.ReparsePoint))throw new IOException("Invalid source");
            files[name]=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file.FullName)));
        }
        var directory=Directory.CreateDirectory(HelperContract.InstallPath);
        var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);acl.SetOwner(new SecurityIdentifier("S-1-5-32-544"));
        foreach(string sid in new[]{"S-1-5-18","S-1-5-32-544"})acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(owner),FileSystemRights.ReadAndExecute,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        directory.SetAccessControl(acl);
        foreach(string name in HelperContract.Files) {
            var file=new FileInfo(Path.Combine(directory.FullName,name));
            File.Copy(Path.Combine(source.FullName,name),file.FullName,false);SetOwner(file);
            if(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file.FullName)))!=files[name])throw new IOException("Copy mismatch");
        }
        var manifestFile=new FileInfo(Path.Combine(directory.FullName,"manifest.json"));
        File.WriteAllText(manifestFile.FullName,JsonSerializer.Serialize(new HelperManifest(1,owner,files)));SetOwner(manifestFile);
        if(HelperContract.Manifest(owner) is null)throw new IOException("Install verification failed");
        dynamic definition=service.NewTask(0);
        definition.RegistrationInfo.Description="AI-bot: on-demand PIN/password page selection for the paired device; no credential input or network.";
        definition.Principal.UserId="SYSTEM";definition.Principal.LogonType=5;definition.Principal.RunLevel=1;
        definition.Settings.Enabled=true;definition.Settings.Hidden=true;definition.Settings.AllowDemandStart=true;
        definition.Settings.ExecutionTimeLimit="PT15S";definition.Settings.MultipleInstances=2;
        definition.Settings.DisallowStartIfOnBatteries=false;definition.Settings.StopIfGoingOnBatteries=false;
        dynamic action=definition.Actions.Create(0);
        action.Path=Path.Combine(directory.FullName,"AIBotLoginHelper.exe");action.Arguments=HelperContract.Arguments;action.WorkingDirectory=directory.FullName;
        string security=$"O:BAG:SYD:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGX;;;{owner})";
        folder.RegisterTaskDefinition(HelperContract.TaskName,definition,2,"SYSTEM",null,5,security);
    }

    private static void Remove(dynamic folder,string owner)
    {
        var manifest=HelperContract.Manifest(owner)??throw new IOException("Ownership verification failed");
        dynamic task=folder.GetTask(HelperContract.TaskName);
        if((string)task.Definition.Principal.UserId is not ("SYSTEM" or "S-1-5-18") || task.Definition.Actions.Count!=1 ||
            !string.Equals((string)task.Definition.Actions.Item(1).Path,Path.Combine(HelperContract.InstallPath,"AIBotLoginHelper.exe"),StringComparison.OrdinalIgnoreCase) ||
            (string)task.Definition.Actions.Item(1).Arguments!=HelperContract.Arguments) throw new IOException("Task mismatch");
        if(task.GetInstances(0).Count!=0)throw new IOException("Helper busy");
        var directory=new DirectoryInfo(HelperContract.InstallPath);
        string[] allowed=[..HelperContract.Files,"manifest.json","result.json","result.json.tmp"];
        var children=directory.GetFileSystemInfos();
        if(children.Any(file=>file is DirectoryInfo || file.Attributes.HasFlag(FileAttributes.ReparsePoint)||!allowed.Contains(file.Name)))throw new IOException("Unexpected file retained");
        folder.DeleteTask(HelperContract.TaskName,0);
        foreach(var file in children)File.Delete(file.FullName);
        Directory.Delete(directory.FullName,false);
    }
}
