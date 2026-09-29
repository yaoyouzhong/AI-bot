using System.Text.Json;
namespace AIBotBridge;

// Called under the reader gate. Only assistant-facing text is retained in this
// bounded cache; it never resumes a thread or claims a stored turn is running.
internal sealed class Tab5StoredReplies {
    internal sealed record Selection(Tab5ActivityPage Page,string LatestId,string LatestState,bool HasPrevious,bool HasNext) {
        internal string PreviousTurnId {get;init;}="";
        internal string NextTurnId {get;init;}="";
        internal Selection[] Neighbors {get;init;}=[];
    }
    private sealed record Cached(long Length,DateTime Stamp,List<Tab5ActivityPage[]> Turns);
    private readonly Dictionary<string,Cached> _cache=new();
    internal Selection? Read(string? path,string home,string turn,int offset,int page) {
        if(string.IsNullOrEmpty(path))return null;
        string Normalize(string value)=>Path.GetFullPath(value.StartsWith(@"\\?\",StringComparison.Ordinal)?value[4..]:value);
        string full=Normalize(path),root=Normalize(home);
        if(!full.EndsWith(".jsonl",StringComparison.OrdinalIgnoreCase)||!new[]{"sessions","archived_sessions"}.Any(d=>full.StartsWith(Path.Combine(root,d)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))return null;
        var info=new FileInfo(full);if(!info.Exists)return null;
        if(!_cache.TryGetValue(full,out var cache)||cache.Length!=info.Length||cache.Stamp!=info.LastWriteTimeUtc) {
            var turns=new List<Tab5ActivityPage[]>();var activity=new Tab5CodexActivity();
            void Save() {
                if(!activity.HasText||activity.View() is not {} v)return;
                var pages=Enumerable.Range(0,v.PageCount).Select(p=>activity.View(p)!).ToArray();
                // An absent completion record is unknown, not interrupted/running.
                for(int i=0;i<pages.Length;i++)if(pages[i].State=="inProgress")pages[i]=pages[i] with {State="stored"};
                int index=turns.FindIndex(t=>t[0].TurnId==v.TurnId);if(index>=0)turns[index]=pages;else turns.Add(pages);
            }
            using var stream=new FileStream(full,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            using var reader=new StreamReader(stream);
            while(reader.ReadLine() is {} line) {
                try {using var doc=JsonDocument.Parse(line);var r=doc.RootElement;
                    if(Tab5CodexDesktop.Text(r,"type")=="event_msg"&&Tab5CodexDesktop.Text(Tab5CodexDesktop.Child(r,"payload"),"type")=="task_started")Save();
                }catch(JsonException){continue;}
                activity.Accept(line);
            }
            Save();cache=new(info.Length,info.LastWriteTimeUtc,turns);
            if(_cache.Count>=4&&!_cache.ContainsKey(full))_cache.Remove(_cache.Keys.First());
            _cache[full]=cache;
        }
        if(cache.Turns.Count==0)return null;
        int selected=turn.Length==0?cache.Turns.Count-1:cache.Turns.FindIndex(t=>t[0].TurnId==turn);
        if(selected<0)throw new Tab5CodexDesktop.Rejected("history_turn_unavailable");
        selected=Math.Clamp(selected+offset,0,cache.Turns.Count-1);
        var chosen=cache.Turns[selected];int p=page<0?chosen.Length-1:Math.Clamp(page,0,chosen.Length-1);
        var latest=cache.Turns[^1][0];
        Selection At(int index,int atPage)=>new(cache.Turns[index][atPage],latest.TurnId,latest.State,index>0,index<cache.Turns.Count-1) {
            PreviousTurnId=index>0?cache.Turns[index-1][0].TurnId:"",
            NextTurnId=index+1<cache.Turns.Count?cache.Turns[index+1][0].TurnId:""
        };
        return At(selected,p) with {Neighbors=new[]{selected-1,selected+1,selected-2,selected-3}
            .Where(i=>i>=0&&i<cache.Turns.Count).Select(i=>At(i,0)).ToArray()};
    }
}
