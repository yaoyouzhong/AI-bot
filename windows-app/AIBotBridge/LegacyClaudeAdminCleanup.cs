using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using AIBot.LoginMethod;

namespace AIBotBridge;

// Migration-only cleanup of the previously installed Claude terminal broker.
// Never installs or runs the old component.
internal static class LegacyClaudeAdminCleanup
{
    internal sealed record Manifest(int Version,string Owner,Dictionary<string,string> Files);
    internal static string RootPath=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"AI-bot-ClaudeTerminal");
    internal static string TaskName(string sid)=>"AI-bot-ClaudeTerminal-"+sid;
    internal const string Arguments="$(Arg0) $(Arg1) $(Arg2)";
    private static readonly string[] Required=["AIBotBridge.exe","AIBotBridge.dll","AIBotBridge.deps.json","AIBotBridge.runtimeconfig.json","AIBotAdminLauncher.exe"];
    private static string Owner=>WindowsIdentity.GetCurrent().User?.Value??throw new IOException("admin_owner_unavailable");
    internal static string PayloadIdentity(IReadOnlyDictionary<string,string> files)=>Convert.ToHexString(SHA256.HashData(
        System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(files.OrderBy(p=>p.Key,StringComparer.OrdinalIgnoreCase)
            .Select(p=>new[]{p.Key.ToLowerInvariant().Replace('/','\\'),p.Value.ToUpperInvariant()})))));
    internal static string Resolve(string root,string relative){
        if(string.IsNullOrEmpty(relative)||Path.IsPathFullyQualified(relative)||relative.Contains(':')||relative.Split('\\','/').Any(p=>p is "" or "." or ".."))throw new IOException("admin_path_invalid");
        string path=Path.GetFullPath(Path.Combine(root,relative));
        if(!path.StartsWith(Path.TrimEndingDirectorySeparator(Path.GetFullPath(root))+"\\",StringComparison.OrdinalIgnoreCase))throw new IOException("admin_path_invalid");
        return path;
    }
    internal static bool OwnedDirectory(string path,string owner)=>Path.GetDirectoryName(Path.GetFullPath(path))?.Equals(RootPath,StringComparison.OrdinalIgnoreCase)==true&&
        Path.GetFileName(path) is {} name&&name.StartsWith("v1-",StringComparison.Ordinal)&&name.EndsWith("-"+owner,StringComparison.Ordinal)&&
        name.Length==3+64+1+owner.Length&&name.AsSpan(3,64).IndexOfAnyExcept("0123456789ABCDEF".AsSpan())<0;
    internal static Manifest? ReadTrusted(string owner,string? directory=null){
        try{
            if(directory is null)return null;
            if(!OwnedDirectory(directory,owner)||!HelperContract.Trusted(new DirectoryInfo(RootPath)))return null;
            var root=new DirectoryInfo(directory);if(!HelperContract.Trusted(root))return null;
            var file=new FileInfo(Path.Combine(root.FullName,"manifest.json"));if(!file.Exists||file.Length>131072||!HelperContract.Trusted(file))return null;
            var manifest=JsonSerializer.Deserialize<Manifest>(File.ReadAllBytes(file.FullName));
            if(manifest is not {Version:1,Files:not null}||manifest.Owner!=owner||manifest.Files.Count is <5 or >256||Required.Any(r=>!manifest.Files.ContainsKey(r)))return null;
            if(manifest.Files.Values.Any(h=>h is not {Length:64}||!h.All(Uri.IsHexDigit))||
                Path.GetFileName(directory).Substring(3,64)!=PayloadIdentity(manifest.Files))return null;
            foreach(var pair in manifest.Files){
                string path=Resolve(root.FullName,pair.Key);var item=new FileInfo(path);
                if(!item.Exists||item.Length>64*1024*1024||!HelperContract.Trusted(item))return null;
                for(var ancestor=item.Directory;ancestor is not null&&!ancestor.FullName.Equals(root.FullName,StringComparison.OrdinalIgnoreCase);ancestor=ancestor.Parent)
                    if(!HelperContract.Trusted(ancestor))return null;
                using var input=item.OpenRead();
                if(!string.Equals(Convert.ToHexString(SHA256.HashData(input)),pair.Value,StringComparison.OrdinalIgnoreCase))return null;
            }
            return manifest;
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or CryptographicException or System.Security.SecurityException){return null;}
    }
    private static dynamic Connect(){dynamic service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service",true)!)!;service.Connect();return service;}
    internal static bool SameSid(string user,string owner){
        if(user.StartsWith("S-1-",StringComparison.OrdinalIgnoreCase))return user==owner;
        try{return new NTAccount(user).Translate(typeof(SecurityIdentifier)).Value==owner;}
        catch(IdentityNotMappedException) when(!user.Contains('\\')&&!user.Contains('@')){
            // Scheduler can return an unqualified local account name. When it
            // is also the computer name, unqualified LookupAccountName fails.
            // Qualify locally and still require the exact original owner SID.
            return new NTAccount(Environment.MachineName,user).Translate(typeof(SecurityIdentifier)).Value==owner;
        }
    }
    internal static bool TaskAclMatches(string security,string owner){
        var descriptor=new RawSecurityDescriptor(security);
        if(descriptor.Owner?.Value is not ("S-1-5-18" or "S-1-5-32-544")||descriptor.DiscretionaryAcl is null)return false;
        bool ownerAccess=false;
        foreach(GenericAce ace in descriptor.DiscretionaryAcl){
            if(ace is not CommonAce common||common.IsCallback)return false;
            if(common.AceQualifier!=AceQualifier.AccessAllowed)continue;
            string sid=common.SecurityIdentifier.Value;
            if(sid is "S-1-5-18" or "S-1-5-32-544")continue;
            uint mask=(uint)common.AccessMask;
            if((mask&0x80000000)!=0)mask=(mask&~0x80000000u)|0x00120089;
            if((mask&0x20000000)!=0)mask=(mask&~0x20000000u)|0x001200a0;
            if(sid!=owner||(mask&~0x001200a9u)!=0)return false;
            ownerAccess|=(mask&0x001200a9)==0x001200a9;
        }
        return ownerAccess;
    }
    internal static bool TaskMatches(dynamic task,string owner,string? directory=null){
        try{
            if(directory is null)return false;
            dynamic definition=task.Definition;
            if(!(bool)task.Enabled||!SameSid((string)definition.Principal.UserId,owner)||(int)definition.Principal.LogonType!=3||(int)definition.Principal.RunLevel!=1||
                (int)definition.Actions.Count!=1||(int)definition.Triggers.Count!=0)return false;
            dynamic action=definition.Actions.Item(1);
            return TaskAclMatches((string)task.GetSecurityDescriptor(7),owner)&&(int)action.Type==0&&string.Equals((string)action.Path,Path.Combine(directory,"AIBotAdminLauncher.exe"),StringComparison.OrdinalIgnoreCase)&&
                (string)action.Arguments==Arguments&&string.Equals((string)action.WorkingDirectory,directory,StringComparison.OrdinalIgnoreCase);
        }catch(Exception ex) when(ex is COMException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException or InvalidCastException or ArgumentException or IdentityNotMappedException or System.Security.SecurityException){return false;}
    }
    // Only a complete manifest belonging to this owner may be removed. No
    // recursive deletion of unrecognized files, reparse points or shared roots.
    private static void DeletePayload(string path,string owner,IReadOnlyCollection<string> files){
        if(!OwnedDirectory(path,owner)||!HelperContract.Trusted(new DirectoryInfo(path)))throw new IOException("admin_cleanup_untrusted");
        var entries=new List<FileSystemInfo>();var pending=new Stack<DirectoryInfo>();pending.Push(new(path));
        while(pending.TryPop(out var directory))foreach(var entry in directory.EnumerateFileSystemInfos()){
            if(entries.Count>=512||!HelperContract.Trusted(entry)||entry is FileInfo&&!files.Contains(Path.GetRelativePath(path,entry.FullName),StringComparer.OrdinalIgnoreCase))
                throw new IOException("admin_cleanup_unknown_files");
            entries.Add(entry);if(entry is DirectoryInfo child)pending.Push(child);
        }
        foreach(var file in entries.OfType<FileInfo>())file.Delete();
        foreach(var dir in entries.OfType<DirectoryInfo>().OrderByDescending(d=>d.FullName.Length))dir.Delete();
        Directory.Delete(path);
    }
    internal static int Remove(){
        try{
            if(!BridgeElevation.IsAdministrator)return 1;string owner=Owner;
            if(!Directory.Exists(RootPath))return 0;
            foreach(var process in Process.GetProcessesByName("AIBotBridge"))using(process){
                try{if(process.MainModule?.FileName is {} path&&path.StartsWith(RootPath+"\\",StringComparison.OrdinalIgnoreCase))return 1;}
                catch(System.ComponentModel.Win32Exception){return 1;}
                catch(InvalidOperationException){}
            }
            dynamic service=Connect();dynamic folder=service.GetFolder("\\");dynamic? task=StartupRegistration.FindNamedTask(folder,TaskName(owner));if(task is null)return 0;
            string active=(string)task.Definition.Actions.Item(1).WorkingDirectory;
            if(ReadTrusted(owner,active) is null||!TaskMatches(task,owner,active)||(int)task.State==4)return 1;
            var payloads=Directory.EnumerateDirectories(RootPath).Where(p=>OwnedDirectory(p,owner)).Select(p=>(Path:p,Manifest:ReadTrusted(owner,p))).ToArray();
            if(payloads.Any(p=>p.Manifest is null))return 1;
            folder.DeleteTask(TaskName(owner),0);
            foreach(var payload in payloads)DeletePayload(payload.Path,owner,[..payload.Manifest!.Files.Keys,"manifest.json"]);
            if(!Directory.EnumerateFileSystemEntries(RootPath).Any())Directory.Delete(RootPath);
            return 0;
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or COMException or ArgumentException or InvalidOperationException or System.Security.SecurityException or System.ComponentModel.Win32Exception or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException){return 1;}
    }

}
