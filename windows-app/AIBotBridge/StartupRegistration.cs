using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Security.Principal;

namespace AIBotBridge;

internal static class StartupRegistration
{
    private const string KeyPath=@"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName="AI-bot";
    private static string UserSid=>BridgeElevation.Owner;
    internal static string TaskName=>"AI-bot-Logon-"+UserSid;
    private static dynamic Connect(){dynamic service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service",true)!)!;service.Connect();return service;}
    internal static dynamic? FindTask(dynamic folder)=>FindNamedTask(folder,TaskName);
    internal static dynamic? FindNamedTask(dynamic folder,string name){
        try{return folder.GetTask(name);}
        catch(Exception ex) when(ex is COMException or FileNotFoundException&&ex.HResult==unchecked((int)0x80070002)){return null;}
    }
    internal static bool Matches(dynamic task,string? executable=null){
        try{
            dynamic definition=task!.Definition;
            if(!(bool)task.Enabled||!LegacyClaudeAdminCleanup.SameSid((string)definition.Principal.UserId,UserSid)||
                (int)definition.Principal.LogonType!=3||(int)definition.Principal.RunLevel!=1||
                (int)definition.Actions.Count!=1||(int)definition.Triggers.Count!=1||
                !LegacyClaudeAdminCleanup.TaskAclMatches((string)task.GetSecurityDescriptor(7),UserSid))return false;
            dynamic action=definition.Actions.Item(1),trigger=definition.Triggers.Item(1);
            string path=(string)action.Path;
            return (int)action.Type==0&&(int)trigger.Type==9&&LegacyClaudeAdminCleanup.SameSid((string)trigger.UserId,UserSid)&&
                string.IsNullOrEmpty((string)action.Arguments)&&BridgeInstallation.TrustedExecutable(path,UserSid)&&
                string.Equals((string)action.WorkingDirectory,Path.GetDirectoryName(path),StringComparison.OrdinalIgnoreCase)&&
                (executable is null||path.Equals(executable,StringComparison.OrdinalIgnoreCase));
        }catch(Exception ex) when(ex is COMException or UnauthorizedAccessException or ArgumentException or IdentityNotMappedException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){return false;}
    }
    internal static bool IsEnabled{
        get{
            try{dynamic service=Connect();dynamic? task=FindTask(service.GetFolder("\\"));
                if(task is not null&&Matches(task))return (bool)task!.Definition.Triggers.Item(1).Enabled;
            }catch(Exception ex) when(ex is COMException or UnauthorizedAccessException){}
            try{using var key=Registry.CurrentUser.OpenSubKey(KeyPath);return key?.GetValue(ValueName) is string value&&value=="\""+Environment.ProcessPath+"\"";}
            catch(Exception ex) when(ex is UnauthorizedAccessException or System.Security.SecurityException or IOException){return false;}
        }
    }
    internal static void ConfigureInstalled(string executable){
        dynamic service=Connect();dynamic? task=FindTask(service.GetFolder("\\"));
        if(task is not null&&Matches(task,executable))return;
        // Preserve a deliberate disabled trigger while updating the product.
        bool enabled=task is null||!Matches(task)||(bool)task!.Definition.Triggers.Item(1).Enabled;
        Register(executable,enabled);
    }
    private static void Register(string executable,bool enabled){
        if(!BridgeElevation.IsAdministrator||!BridgeInstallation.TrustedExecutable(executable,UserSid))throw new UnauthorizedAccessException("Protected administrator bridge required");
        dynamic service=Connect(),definition=service.NewTask(0);
        definition.RegistrationInfo.Description="AI-bot：当前用户登录后以管理员权限在托盘后台运行。";
        definition.Principal.UserId=UserSid;definition.Principal.LogonType=3;definition.Principal.RunLevel=1;
        // Register disabled until its owner and DACL are independently verified.
        definition.Settings.Enabled=false;definition.Settings.AllowDemandStart=true;
        definition.Settings.DisallowStartIfOnBatteries=false;definition.Settings.StopIfGoingOnBatteries=false;
        definition.Settings.ExecutionTimeLimit="PT0S";definition.Settings.MultipleInstances=2;
        definition.Settings.StartWhenAvailable=true;
        dynamic trigger=definition.Triggers.Create(9);trigger.UserId=UserSid;trigger.Enabled=enabled;trigger.Delay="PT5S";
        dynamic action=definition.Actions.Create(0);action.Path=executable;action.WorkingDirectory=Path.GetDirectoryName(executable);
        string security=$"O:BAG:SYD:P(A;;GA;;;SY)(A;;GA;;;BA)(A;;GRGX;;;{UserSid})";
        dynamic folder=service.GetFolder("\\");dynamic? task=null;
        try{
            task=folder.RegisterTaskDefinition(TaskName,definition,6|0x10,UserSid,null,3,security);
            // Updating an existing task can retain its previous descriptor.
            // Explicitly replace it before allowing logon or demand launch.
            task.SetSecurityDescriptor(security,0x10);
            if(!LegacyClaudeAdminCleanup.TaskAclMatches((string)task.GetSecurityDescriptor(7),UserSid))throw new IOException("Administrator startup ACL verification failed");
            task.Enabled=true;
            if(!Matches(task,executable)||(bool)task.Definition.Triggers.Item(1).Enabled!=enabled)throw new IOException("Administrator startup verification failed");
        }catch{
            try{if(task is not null)task.Enabled=false;}catch(Exception ex) when(ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){}
            throw;
        }
        using var key=Registry.CurrentUser.OpenSubKey(KeyPath,true);key?.DeleteValue(ValueName,false);
    }
    internal static void SetEnabled(bool enabled){
        dynamic service=Connect();dynamic? task=FindTask(service.GetFolder("\\"));
        string path=task is not null&&Matches(task)?(string)task!.Definition.Actions.Item(1).Path:BridgeInstallation.Configure();
        Register(path,enabled);
    }
    internal static bool TryStartInstalled(){
        dynamic service=Connect();dynamic? task=FindTask(service.GetFolder("\\"));
        if(task is null||!Matches(task))return false;
        string path=(string)task!.Definition.Actions.Item(1).Path;
        if(BridgeInstallation.Read(path,UserSid) is not {} manifest||!BridgeInstallation.MatchesSource(manifest))return false;
        task!.Run(null);return true;
    }
    internal static void StartInstalled(string executable){
        dynamic service=Connect();dynamic? task=FindTask(service.GetFolder("\\"));
        if(task is null||!Matches(task,executable))throw new IOException("Administrator startup changed");
        task!.Run(null);
    }
    internal static void RemoveInstalled(){
        if(!BridgeElevation.IsAdministrator)throw new UnauthorizedAccessException();
        dynamic service=Connect(),folder=service.GetFolder("\\");dynamic? task=FindTask(folder);
        if(task is null)return;if(!Matches(task))throw new IOException("Unrecognized startup retained");folder.DeleteTask(TaskName,0);
    }
}
