using System.IO;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text.Json;

namespace AIBot.LoginMethod;

internal enum LoginMethodKind : byte { Manual, Pin, Password }
internal sealed record HelperManifest(int Version, string OwnerSid, Dictionary<string,string> Files);
internal sealed record HelperReply(int Version, string Nonce, int Session, string Target, bool Ready, long CompletedUtcMs, int ElapsedMs, string Status);
internal static class HelperContract
{
    internal const string TaskName = "AIBot-LoginMethodHelper";
    internal const string Arguments = "--prepare $(Arg0) $(Arg1) $(Arg2) $(Arg3)";
    internal static readonly string InstallPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "AI-bot-LoginHelper");
    internal static readonly string[] Files = ["AIBotLoginHelper.exe", "AIBotLoginHelper.dll", "AIBotLoginHelper.deps.json", "AIBotLoginHelper.runtimeconfig.json"];
    internal static string Target(LoginMethodKind kind) => kind switch { LoginMethodKind.Pin => "pin", LoginMethodKind.Password => "password", _ => "manual" };
    internal static bool Fresh(long issued, long now) => issued > 0 && now >= issued - 1000 && now - issued < 5000;
    internal static bool Accept(HelperReply? reply, string nonce, int session, string target, long issued, long now) =>
        reply is {Version:1,Ready:true,Status:"ready"} && reply.Nonce == nonce && reply.Session == session && reply.Target == target &&
        reply.CompletedUtcMs >= issued && reply.CompletedUtcMs <= now + 1000 && now - reply.CompletedUtcMs < 1500 && Fresh(issued, now);

    internal static bool Trusted(FileSystemInfo item)
    {
        if (!item.Exists || item.Attributes.HasFlag(FileAttributes.ReparsePoint)) return false;
        FileSystemSecurity acl = item is DirectoryInfo directory ? directory.GetAccessControl() : ((FileInfo)item).GetAccessControl();
        static bool Admin(string? sid) => sid is "S-1-5-18" or "S-1-5-32-544";
        if (!Admin(acl.GetOwner(typeof(SecurityIdentifier))?.Value)) return false;
        const FileSystemRights write = FileSystemRights.Write | FileSystemRights.Delete | FileSystemRights.DeleteSubdirectoriesAndFiles | FileSystemRights.ChangePermissions | FileSystemRights.TakeOwnership;
        foreach (FileSystemAccessRule rule in acl.GetAccessRules(true, true, typeof(SecurityIdentifier)))
            if (rule.AccessControlType == AccessControlType.Allow && !Admin(rule.IdentityReference.Value) && (rule.FileSystemRights & write) != 0) return false;
        return true;
    }

    internal static HelperManifest? Manifest(string? owner = null)
    {
        try {
            if (!Trusted(new DirectoryInfo(InstallPath))) return null;
            var file = new FileInfo(Path.Combine(InstallPath,"manifest.json"));
            if (file.Length > 8192 || !Trusted(file)) return null;
            var manifest = JsonSerializer.Deserialize<HelperManifest>(File.ReadAllText(file.FullName));
            if (manifest is not {Version:1,Files:not null} || string.IsNullOrEmpty(manifest.OwnerSid) || (owner is not null && manifest.OwnerSid != owner) || manifest.Files.Count != Files.Length ||
                manifest.Files.Keys.Except(Files).Any()) return null;
            _ = new SecurityIdentifier(manifest.OwnerSid);
            foreach (string name in Files) {
                var runtime = new FileInfo(Path.Combine(InstallPath,name));
                if (!Trusted(runtime) || !string.Equals(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(runtime.FullName))), manifest.Files[name], StringComparison.OrdinalIgnoreCase)) return null;
            }
            return manifest;
        } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or System.Security.SecurityException) { return null; }
    }
}
