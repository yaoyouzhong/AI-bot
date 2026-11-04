using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
namespace AIBotBridge;

internal sealed class ClaudeReplyReader(Func<bool> enabled,string? directory=null)
{
    private sealed record Binding(Guid Session,string Title,DateTimeOffset Touched);
    private readonly object _sync=new();
    private readonly Dictionary<string,Binding> _bindings=new();
    private readonly string _root=directory??Path.Combine(AppPaths.GetFolderPath(Environment.SpecialFolder.UserProfile),".claude","projects");
    internal bool IsEnabled=>enabled();
    internal void Invalidate(){lock(_sync)_bindings.Clear();}
    internal string? Advertise(Guid? session,string? title,DateTimeOffset now)
    {
        if(!enabled()||session is null){Invalidate();return null;}
        lock(_sync){
            foreach(var key in _bindings.Where(p=>now-p.Value.Touched>=TimeSpan.FromMinutes(10)).Select(p=>p.Key).ToArray())_bindings.Remove(key);
            var binding=_bindings.FirstOrDefault(p=>p.Value.Session==session).Key??Guid.NewGuid().ToString("N");
            if(_bindings.Count>=32&&!_bindings.ContainsKey(binding))_bindings.Remove(_bindings.MinBy(p=>p.Value.Touched).Key);
            _bindings[binding]=new(session.Value,title??"Claude Code",now);return binding;
        }
    }
    internal (int Status,object Body) Handle(JsonElement request,DateTimeOffset? clock=null)
    {
        var now=clock??DateTimeOffset.UtcNow;
        (int,object) Error(int code,string error)=>(code,new{error});
        if(!enabled()){Invalidate();return Error(403,"claude_replies_disabled");}
        if(Text(request,"op")!="read"||Text(request,"binding") is not {} key||!request.TryGetProperty("page",out var pageValue)||pageValue.ValueKind!=JsonValueKind.Number||!pageValue.TryGetInt32(out int page)||page is < -1 or >4095)return Error(400,"invalid_read");
        Binding? bound;lock(_sync)bound=_bindings.GetValueOrDefault(key);
        if(bound is null||now-bound.Touched>=TimeSpan.FromMinutes(10))return Error(409,"session_changed");
        try {
            var options=new EnumerationOptions{RecurseSubdirectories=true,IgnoreInaccessible=true,AttributesToSkip=FileAttributes.ReparsePoint};
            string? target=null;
            foreach(var path in Directory.EnumerateFiles(_root,"*.jsonl",options)){
                if(path.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Any(p=>p.Equals("subagents",StringComparison.OrdinalIgnoreCase)))continue;
                if(Guid.TryParse(Path.GetFileNameWithoutExtension(path),out var id)&&id==bound.Session){if(target is not null)return Error(503,"claude_reply_unavailable");target=path;}
            }
            if(target is null)return Error(503,"claude_reply_unavailable");
            var reply=ClaudePublicReplies.Read(target,bound.Session);
            string revision=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(reply.Text))).ToLowerInvariant();
            if(Text(request,"revision") is {Length:>0} expected&&expected!=revision)return Error(409,"reply_changed");
            var pages=new List<string>();var current=new StringBuilder();int bytes=0;
            foreach(var rune in reply.Text.EnumerateRunes()){
                if(bytes+rune.Utf8SequenceLength>1600){pages.Add(current.ToString());current.Clear();bytes=0;}
                current.Append(rune.ToString());bytes+=rune.Utf8SequenceLength;
            }
            if(current.Length>0||pages.Count==0)pages.Add(current.ToString());
            int selected=page<0?pages.Count-1:page;
            if(selected>=pages.Count)return Error(409,"reply_changed");
            if(!enabled()){Invalidate();return Error(403,"claude_replies_disabled");}
            lock(_sync)if(!_bindings.TryGetValue(key,out var latest)||latest.Session!=bound.Session)return Error(409,"session_changed");
            return (200,new{version=1,binding=key,revision,title=bound.Title,text=pages[selected],page=selected,total=pages.Count,hasEarlier=reply.HasEarlier});
        }catch(Exception e) when(e is IOException or UnauthorizedAccessException or JsonException){return Error(503,"claude_reply_unavailable");}
    }
    private static string? Text(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
}
