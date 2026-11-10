using System.Security.Cryptography;
using System.Text;
namespace AIBotBridge;

internal sealed class ClaudeCatalog(string root,Func<byte[]> key,Func<IEnumerable<ClaudeSessionObservation>>? newPanels=null,
    Func<IReadOnlySet<Guid>,IEnumerable<ClaudeSessionObservation>>? discoverMissing=null)
{
    internal sealed record Entry(string Alias,Guid Session,string Directory,string Title,long Updated,string Project,string Folder);
    private readonly ClaudeSessionIndex _index=new(root);
    private readonly object _sync=new();private Entry[]? _entries;
    private readonly Tab5CatalogDisplay<Entry> _display=new();
    internal static string Alias(string value,byte[] key,bool uuid=true){
        string hex=Convert.ToHexString(HMACSHA256.HashData(key,Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
        return uuid?Guid.ParseExact(hex[..32],"N").ToString():hex[..16];
    }
    internal bool Refresh(DateTimeOffset? clock=null){
        try {RefreshCore(clock);return true;}
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or CryptographicException) {
            lock(_sync){_entries=null;_display.Observe(null);}
            return false;
        }
    }
    private void RefreshCore(DateTimeOffset? clock){
        _=Directory.GetFileSystemEntries(root);byte[] secret=key();if(secret.Length!=32)throw new IOException("private key unavailable");
        var observed=_index.ScanAll(clock??DateTimeOffset.UtcNow);
        var known=observed.Select(row=>row.Id).ToHashSet();
        var missing=discoverMissing?.Invoke(known)??newPanels?.Invoke()??[];
        var rows=observed.Concat(missing).DistinctBy(row=>row.Id).OrderByDescending(row=>row.LastActivity).Select(r=>{
            string directory=r.Directory is {Length:>0}&&Path.IsPathFullyQualified(r.Directory)?Path.GetFullPath(r.Directory):"";
            // Windows cwd spelling is not identity: CLI and VS Code commonly
            // report the same directory with different drive/folder casing.
            string project=directory.Length>0?Path.TrimEndingDirectorySeparator(directory).ToUpperInvariant():"unknown";
            string title=(known.Contains(r.Id)?r.Title:_index.KnownTitle(r.Id)??r.Title)??"Claude Code";
            return new Entry(Alias("claude:session:"+r.Id.ToString(),secret),r.Id,directory,title,r.LastActivity.ToUnixTimeSeconds(),Alias("claude:project:"+project,secret,false),directory.Length>0?Path.GetFileName(Path.TrimEndingDirectorySeparator(directory)):"项目待确认");
        }).ToArray();
        lock(_sync){_entries=rows;_display.Observe(rows);}
    }
    internal IReadOnlyList<Entry> Snapshot(){lock(_sync)return _entries??[];}
    internal (IReadOnlyList<Entry> Items,bool Fresh) DisplaySnapshot(){lock(_sync)return _entries is {Length:>0} rows?(rows,true):_display.Observe(_entries);}
    internal void Clear(){lock(_sync){_entries=null;_display.Clear();}}
    internal Entry? Resolve(string alias){lock(_sync)return _entries?.FirstOrDefault(e=>e.Alias.Equals(alias,StringComparison.OrdinalIgnoreCase));}
}
