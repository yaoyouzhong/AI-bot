using System.Security.Cryptography;
using System.Text;

namespace AIBotBridge;

// Resolve only locally verifiable linked worktrees. A name match is not proof.
internal static class Tab5GitProject
{
    private static readonly StringComparer Paths=StringComparer.OrdinalIgnoreCase;
    internal static string Id(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..16];
    internal static string? CommonDirectory(string root)
    {
        try {
            var dotGit=Path.Combine(root,".git");
            if(Directory.Exists(dotGit))return dotGit;
            string? pointer=Read(dotGit);
            if(pointer is null||!pointer.StartsWith("gitdir: ",StringComparison.Ordinal))return null;
            string gitDir=Path.TrimEndingDirectorySeparator(Path.GetFullPath(pointer[8..],root));
            string? relative=Read(Path.Combine(gitDir,"commondir")),backlink=Read(Path.Combine(gitDir,"gitdir"));
            if(relative is null||backlink is null)return null;
            string common=Path.TrimEndingDirectorySeparator(Path.GetFullPath(relative,gitDir));
            return Directory.Exists(common)&&Paths.Equals(Path.GetDirectoryName(gitDir),Path.Combine(common,"worktrees"))
                &&Paths.Equals(Path.GetFullPath(backlink,gitDir),dotGit)?common:null;
        } catch(Exception e) when(e is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException){return null;}
    }
    private static string? Read(string path) {
        if(!File.Exists(path))return null;
        using var file=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        if(file.Length is <1 or >4096)return null;
        var bytes=new byte[(int)file.Length];file.ReadExactly(bytes);
        if(file.ReadByte()!=-1)return null;
        string value=new UTF8Encoding(false,true).GetString(bytes).Trim();
        return value.Length>0&&!value.Any(char.IsControl)?value:null;
    }
}
