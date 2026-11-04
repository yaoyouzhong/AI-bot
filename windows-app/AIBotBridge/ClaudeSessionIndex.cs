using System.Globalization;
using System.Text;
using System.Text.Json;

namespace AIBotBridge;

// Only existing title records and activity metadata are retained. Raw messages,
// paths and session identifiers are never returned to the device.
internal sealed record ClaudeSessionObservation(Guid Id, DateTimeOffset LastActivity, string? Title,string? Directory=null);
internal sealed class ClaudeSessionIndex(string root)
{
    private sealed record Entry(long Length, DateTime Modified, ClaudeSessionObservation? Value, DateTimeOffset? RetryAt);
    private readonly Dictionary<string,Entry> _cache=new(StringComparer.OrdinalIgnoreCase);
    internal ClaudeSessionObservation? Scan(DateTimeOffset now,Guid? sessionId=null)
        =>ScanAll(now).FirstOrDefault(value=>sessionId is null||value.Id==sessionId);
    internal IReadOnlyList<ClaudeSessionObservation> ScanAll(DateTimeOffset now)
    {
        if(!Directory.Exists(root)){_cache.Clear();return [];}
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var candidates=new List<ClaudeSessionObservation>();
        var options=new EnumerationOptions {RecurseSubdirectories=true,IgnoreInaccessible=true,AttributesToSkip=FileAttributes.ReparsePoint};
        foreach(var path in Directory.EnumerateFiles(root,"*.jsonl",options)) {
            if(path.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Any(p=>p.Equals("subagents",StringComparison.OrdinalIgnoreCase)) || !Guid.TryParse(Path.GetFileNameWithoutExtension(path),out var id))continue;
            try {
                var file=new FileInfo(path);seen.Add(path);
                if(!_cache.TryGetValue(path,out var entry)||entry.Length!=file.Length||entry.Modified!=file.LastWriteTimeUtc||entry.RetryAt is {} retry&&now>=retry) {
                    var read=Read(path,id,now);entry=new(file.Length,file.LastWriteTimeUtc,read.Value,read.RetryAt);_cache[path]=entry;
                }
                var value=entry.Value;
                if(value is null||value.LastActivity>now.AddSeconds(60))continue;
                candidates.Add(value);
            } catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException){_cache.Remove(path);}
        }
        foreach(var path in _cache.Keys.Where(path=>!seen.Contains(path)).ToArray())_cache.Remove(path);
        return candidates.GroupBy(v=>v.Id).Where(g=>g.Count()==1).Select(g=>g.Single()).OrderByDescending(v=>v.LastActivity).ThenBy(v=>v.Id.ToString(),StringComparer.Ordinal).ToArray();
    }
    private static (ClaudeSessionObservation? Value,DateTimeOffset? RetryAt) Read(string path,Guid id,DateTimeOffset now)
    {
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        using var reader=new StreamReader(stream,Encoding.UTF8,true);
        DateTimeOffset latest=DateTimeOffset.MinValue;DateTimeOffset? retryAt=null;string? custom=null,generated=null,directory=null;
        foreach(string line in CompleteLines(reader)) {
            try {
                using var doc=JsonDocument.Parse(line);var obj=doc.RootElement;
                if(!Guid.TryParse(Text(obj,"sessionId"),out var rowId)||rowId!=id)continue;
                var type=Text(obj,"type");
                if(type=="custom-title"&&line.Length<=16384){custom=Clean(Text(obj,"customTitle"));continue;}
                if(type=="ai-title"&&line.Length<=16384){generated=Clean(Text(obj,"aiTitle"));continue;}
                if(type is not ("user" or "assistant")||obj.TryGetProperty("isSidechain",out var side)&&side.ValueKind==JsonValueKind.True||!DateTimeOffset.TryParse(Text(obj,"timestamp"),CultureInfo.InvariantCulture,DateTimeStyles.RoundtripKind,out var at))continue;
                if(at>now.AddSeconds(60)){var next=at.AddSeconds(-60);if(retryAt is null||next<retryAt)retryAt=next;continue;}
                if(at>=latest){latest=at;var cwd=Text(obj,"cwd");if(cwd is {Length:>0 and <=4096}&&!cwd.Contains('\0')&&Path.IsPathFullyQualified(cwd))directory=cwd;}
            }catch(JsonException){}
        }
        return (latest==DateTimeOffset.MinValue?null:new(id,latest,custom??generated,directory),retryAt);
    }
    internal static IEnumerable<string> CompleteLines(TextReader reader)
    {
        var buffer=new char[65536];var line=new StringBuilder();bool oversized=false;int count;
        while((count=reader.Read(buffer,0,buffer.Length))>0)for(int i=0;i<count;i++){
            char value=buffer[i];
            if(value=='\n'){if(!oversized)yield return line.ToString();line.Clear();oversized=false;}
            else if(!oversized){if(line.Length<2_000_000)line.Append(value);else{line.Clear();oversized=true;}}
        }
        // An unterminated tail may still be being written; retry after append.
    }
    private static string? Text(JsonElement obj,string key)=>obj.ValueKind==JsonValueKind.Object&&obj.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;
    private static string? Clean(string? value)
    {
        if(value is null)return null;
        var words=new string(value.Select(ch=>char.IsControl(ch)?' ':ch).ToArray()).Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
        string text=string.Join(' ',words);if(text.Length==0)return null;
        var boundaries=StringInfo.ParseCombiningCharacters(text);int end=0,bytes=0;
        for(int i=0;i<Math.Min(120,boundaries.Length);i++){
            int start=boundaries[i],next=i+1<boundaries.Length?boundaries[i+1]:text.Length;
            int size=Encoding.UTF8.GetByteCount(text.AsSpan(start,next-start));if(bytes+size>512)break;
            bytes+=size;end=next;
        }
        return end==0?null:text[..end];
    }
}

internal sealed class ClaudeSessionSampler
{
    private readonly ClaudeSessionIndex _index=new(Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","projects"));
    private readonly object _lock=new();private ClaudeSessionObservation? _current;private Guid? _selected;private bool _scanning;private long _next;private int _generation;
    internal ClaudeSessionObservation? Capture(bool enabled,DateTimeOffset now,Guid? sessionId=null)
    {
        lock(_lock) {
            if(!enabled){_current=null;_selected=null;_generation++;_next=0;return null;}
            if(_selected!=sessionId){_selected=sessionId;_current=null;_generation++;_next=0;}
            var value=_current;
            if(!_scanning&&Environment.TickCount64>=_next){_scanning=true;_next=Environment.TickCount64+5000;int generation=_generation;
                _=Task.Run(()=>{ClaudeSessionObservation? next=null;try{next=_index.Scan(now,sessionId);}catch(Exception e) when(e is IOException or UnauthorizedAccessException){}lock(_lock){if(_generation==generation)_current=next;_scanning=false;}});
            }
            return value;
        }
    }
}
