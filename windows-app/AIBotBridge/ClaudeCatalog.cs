using System.Security.Cryptography;
using System.Text;
namespace AIBotBridge;

internal sealed class ClaudeCatalog(string root,Func<byte[]> key)
{
    internal sealed record Entry(string Alias,Guid Session,string Directory,string Title,long Updated,string Project,string Folder);
    private readonly ClaudeSessionIndex _index=new(root);
    private readonly object _sync=new();private Entry[] _entries=[];
    internal static string Alias(string value,byte[] key,bool uuid=true){
        string hex=Convert.ToHexString(HMACSHA256.HashData(key,Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        return uuid?Guid.ParseExact(hex[..32],"N").ToString():hex[..16];
    }
    internal void Refresh(DateTimeOffset? clock=null){
        _=Directory.GetFileSystemEntries(root);byte[] secret=key();if(secret.Length!=32)throw new IOException("private key unavailable");
        var rows=_index.ScanAll(clock??DateTimeOffset.UtcNow).Select(r=>{
            string directory=r.Directory is {Length:>0}&&Path.IsPathFullyQualified(r.Directory)?Path.GetFullPath(r.Directory):"";
            return new Entry(Alias("claude:session:"+r.Id.ToString(),secret),r.Id,directory,r.Title??"Claude Code",r.LastActivity.ToUnixTimeSeconds(),Alias("claude:project:"+(directory.Length>0?directory:"unknown"),secret,false),directory.Length>0?Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)):"项目待确认");
        }).ToArray();
        lock(_sync)_entries=rows;
    }
    internal IReadOnlyList<Entry> Snapshot(){lock(_sync)return _entries.ToArray();}
    internal Entry? Resolve(string alias){lock(_sync)return _entries.FirstOrDefault(e=>e.Alias.Equals(alias,StringComparison.OrdinalIgnoreCase));}
}
