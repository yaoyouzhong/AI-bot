using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;
using AIBot.LoginMethod;

namespace AIBotBridge;

// Portable and installed entry points converge on one protected copy of the
// complete bridge. The scheduled action is that GUI executable, with no args.
internal static class BridgeInstallation
{
    internal sealed record Manifest(int Version,string Owner,Dictionary<string,string> Files,int? HttpPort=null);
    internal static string Root=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"AI-bot");
    private static readonly string[] Required=["AIBotBridge.exe","AIBotBridge.dll","AIBotBridge.deps.json","AIBotBridge.runtimeconfig.json"];
    private static readonly Lazy<Dictionary<string,string>> SourceFiles=new(()=>Payload(AppContext.BaseDirectory).ToDictionary(p=>Path.GetRelativePath(AppContext.BaseDirectory,p),p=>Hash(p),StringComparer.OrdinalIgnoreCase));
    internal static bool MatchesSource(Manifest manifest)=>Identity(manifest.Files)==Identity(SourceFiles.Value);
    private static IEnumerable<string> Payload(string source){
        var files=Directory.EnumerateFiles(source).Where(p=>Required.Contains(Path.GetFileName(p),StringComparer.OrdinalIgnoreCase)||
            Path.GetExtension(p).Equals(".dll",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(p) is "LICENSE" or "THIRD_PARTY_NOTICES.md").ToList();
        foreach(string name in new[]{"runtimes","Assets","LoginHelper","licenses"}){
            string directory=Path.Combine(source,name);if(!Directory.Exists(directory))continue;
            if(new DirectoryInfo(directory).Attributes.HasFlag(FileAttributes.ReparsePoint))throw new IOException("bridge_payload_link");
            files.AddRange(Directory.EnumerateFiles(directory,"*",SearchOption.AllDirectories));
        }
        if(files.Count>10000||Required.Any(n=>!files.Any(f=>Path.GetFileName(f)==n)))throw new IOException("bridge_payload_incomplete");
        return files;
    }
    internal static string Resolve(string root,string relative)=>LegacyClaudeAdminCleanup.Resolve(root,relative);
    internal static string Identity(IReadOnlyDictionary<string,string> files)=>LegacyClaudeAdminCleanup.PayloadIdentity(files);
    private static string Hash(string path){using var file=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(file));}
    internal static bool OwnedPath(string directory,string owner)=>Path.GetDirectoryName(Path.GetFullPath(directory))?.Equals(Root,StringComparison.OrdinalIgnoreCase)==true&&
        Path.GetFileName(directory) is {} name&&name.StartsWith("v1-",StringComparison.Ordinal)&&name.EndsWith("-"+owner,StringComparison.Ordinal)&&
        name.Length==3+64+1+owner.Length&&name.AsSpan(3,64).IndexOfAnyExcept("0123456789ABCDEF".AsSpan())<0;
    internal static bool TrustedExecutable(string executable,string owner){
        try{
            string directory=Path.GetDirectoryName(executable)??"";
            return Path.GetFileName(executable).Equals("AIBotBridge.exe",StringComparison.OrdinalIgnoreCase)&&OwnedPath(directory,owner)&&
                HelperContract.Trusted(new DirectoryInfo(Root))&&HelperContract.Trusted(new DirectoryInfo(directory))&&HelperContract.Trusted(new FileInfo(executable));
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException){return false;}
    }
    internal static Manifest? Read(string executable,string owner){
        try{
            if(!TrustedExecutable(executable,owner))return null;
            string directory=Path.GetDirectoryName(executable)!;var file=new FileInfo(Path.Combine(directory,"bridge-install.json"));
            if(!file.Exists||file.Length>2*1024*1024||!HelperContract.Trusted(file))return null;
            var manifest=JsonSerializer.Deserialize<Manifest>(File.ReadAllBytes(file.FullName));
            if(manifest is not {Version:1,Files:not null}||manifest.Owner!=owner||manifest.Files.Count is <4 or >10000||manifest.HttpPort is <1 or >65535||
                Required.Any(r=>!manifest.Files.ContainsKey(r))||Path.GetFileName(directory).Substring(3,64)!=Identity(manifest.Files))return null;
            foreach(var pair in manifest.Files){
                var item=new FileInfo(Resolve(directory,pair.Key));
                if(!HelperContract.Trusted(item)||Hash(item.FullName)!=pair.Value)return null;
                for(var parent=item.Directory;parent is not null&&!parent.FullName.Equals(directory,StringComparison.OrdinalIgnoreCase);parent=parent.Parent)
                    if(!HelperContract.Trusted(parent))return null;
            }
            return manifest;
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or JsonException or System.Security.SecurityException){return null;}
    }
    private static DirectorySecurity Acl(string owner){
        var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);acl.SetOwner(new SecurityIdentifier("S-1-5-32-544"));
        foreach(string sid in new[]{"S-1-5-18","S-1-5-32-544"})acl.AddAccessRule(new(new SecurityIdentifier(sid),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
        acl.AddAccessRule(new(new SecurityIdentifier(owner),FileSystemRights.ReadAndExecute,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));return acl;
    }
    private static void DirectoryProtected(string path,string owner){
        var directory=new DirectoryInfo(path);
        if(directory.Exists){if(!HelperContract.Trusted(directory))throw new IOException("bridge_existing_directory_untrusted");}
        else directory.Create(Acl(owner));
    }
    private static void SetOwner(FileInfo file){var acl=file.GetAccessControl();acl.SetOwner(new SecurityIdentifier("S-1-5-32-544"));file.SetAccessControl(acl);}
    internal static string Configure(){
        AppProfileGuard.EnsureDesktopProfile();if(!BridgeElevation.IsAdministrator)throw new UnauthorizedAccessException("bridge_administrator_required");
        string owner=BridgeElevation.Owner,source=AppContext.BaseDirectory;
        var files=SourceFiles.Value;
        string directory=Path.Combine(Root,"v1-"+Identity(files)+"-"+owner),executable=Path.Combine(directory,"AIBotBridge.exe");
        DirectoryProtected(Root,owner);
        bool created=!Directory.Exists(directory);
        string destination=created?Path.Combine(Root,".install-"+Guid.NewGuid().ToString("N")):directory;
        DirectoryProtected(destination,owner);
        if(created){
            try{
            foreach(var pair in files){
                string from=Resolve(source,pair.Key),to=Resolve(destination,pair.Key);var item=new FileInfo(from);
                if(item.Attributes.HasFlag(FileAttributes.ReparsePoint))throw new IOException("bridge_payload_link");
                for(var ancestor=item.Directory;ancestor is not null&&!ancestor.FullName.Equals(Path.TrimEndingDirectorySeparator(source),StringComparison.OrdinalIgnoreCase);ancestor=ancestor.Parent)
                    if(ancestor.Attributes.HasFlag(FileAttributes.ReparsePoint))throw new IOException("bridge_payload_link");
                var parents=new Stack<string>();for(string? path=Path.GetDirectoryName(to);path is not null&&path!=destination;path=Path.GetDirectoryName(path))parents.Push(path);
                foreach(string path in parents)DirectoryProtected(path,owner);
                File.Copy(from,to,false);SetOwner(new(to));if(Hash(to)!=pair.Value)throw new IOException("bridge_payload_changed");
            }
            int? port=int.TryParse(Environment.GetEnvironmentVariable("AIBOT_HTTP_PORT"),out int configured)&&configured is >0 and <=65535?configured:null;
            string manifest=Path.Combine(destination,"bridge-install.json");File.WriteAllText(manifest,JsonSerializer.Serialize(new Manifest(1,owner,files,port)));SetOwner(new(manifest));
            Directory.Move(destination,directory);
            }catch{
                // Only this invocation's fresh, administrator-owned staging
                // directory is disposable. Never remove an existing candidate.
                if(Path.GetDirectoryName(destination)==Root&&Path.GetFileName(destination).StartsWith(".install-",StringComparison.Ordinal)&&
                    HelperContract.Trusted(new DirectoryInfo(destination))&&
                    !Directory.EnumerateFileSystemEntries(destination,"*",SearchOption.AllDirectories).Any(p=>File.GetAttributes(p).HasFlag(FileAttributes.ReparsePoint)))
                    Directory.Delete(destination,true);
                throw;
            }
        }
        if(Read(executable,owner) is null)throw new IOException("bridge_install_verification_failed");
        StartupRegistration.ConfigureInstalled(executable);
        int cleanup=LegacyClaudeAdminCleanup.Remove();
        Tab5LocalDiagnostics.Write("BRIDGE_ADMIN stage=configured legacyCleanup="+cleanup);
        return executable;
    }
    internal static void Remove(){
        AppProfileGuard.EnsureDesktopProfile();if(!BridgeElevation.IsAdministrator)throw new UnauthorizedAccessException();
        string owner=BridgeElevation.Owner;if(!Directory.Exists(Root))return;
        var payloads=Directory.EnumerateDirectories(Root).Where(p=>OwnedPath(p,owner))
            .Select(p=>(Directory:p,Manifest:Read(Path.Combine(p,"AIBotBridge.exe"),owner))).ToArray();
        if(payloads.Any(p=>p.Manifest is null))throw new IOException("Unrecognized bridge files retained");
        // Validate every path and inventory before deleting the task or files.
        var entries=new List<FileSystemInfo>();
        foreach(var payload in payloads){
            var allowed=payload.Manifest!.Files.Keys.Append("bridge-install.json").ToHashSet(StringComparer.OrdinalIgnoreCase);
            var pending=new Stack<DirectoryInfo>();pending.Push(new(payload.Directory));
            while(pending.TryPop(out var directory))foreach(var item in directory.EnumerateFileSystemInfos()){
                if(!HelperContract.Trusted(item)||item is FileInfo&&!allowed.Contains(Path.GetRelativePath(payload.Directory,item.FullName)))
                    throw new IOException("Unrecognized bridge files retained");
                entries.Add(item);if(item is DirectoryInfo child)pending.Push(child);
            }
        }
        StartupRegistration.RemoveInstalled();
        foreach(var file in entries.OfType<FileInfo>())file.Delete();
        foreach(var directory in entries.OfType<DirectoryInfo>().OrderByDescending(d=>d.FullName.Length))directory.Delete();
        foreach(var payload in payloads)Directory.Delete(payload.Directory);
        _=LegacyClaudeAdminCleanup.Remove();
    }
}
