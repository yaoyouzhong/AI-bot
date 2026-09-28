using System.Text;
using System.Text.Json;

namespace AIBotBridge;

internal sealed record Tab5ActivityPage(string TurnId,string State,string Text,int Page,int PageCount,long StartedAt,bool HasReply,bool HasText=false);

// Desktop rollouts are append-only. Cache the cursor, not a repeatedly scanned
// tail: a long tool result can push the current turn's start out of any tail.
internal sealed class Tab5CodexActivity
{
    private string _path="",_turn="",_state="";
    private long _offset,_startedAt;
    private readonly List<string> _pages=[];
    private readonly HashSet<string> _seen=[];
    private string? _lastMessage;
    private bool _hasReply;
    private string? _targetTurn;
    private bool _acceptTurn=true;
    internal Tab5ActivityPage? Read(string path,string home,int page=-1,string? targetTurn=null) {
        try {
            string Normalize(string value)=>Path.GetFullPath(value.StartsWith(@"\\?\",StringComparison.Ordinal)?value[4..]:value);
            string full=Normalize(path),root=Normalize(home);
            if(!full.EndsWith(".jsonl",StringComparison.OrdinalIgnoreCase)||
               !new[]{"sessions","archived_sessions"}.Any(dir=>full.StartsWith(Path.Combine(root,dir)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)))return null;
            using var file=new FileStream(full,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
            if(full!=_path||file.Length<_offset||_targetTurn!=targetTurn) {Reset("");_offset=0;_path=full;_targetTurn=targetTurn;_acceptTurn=targetTurn is null;}
            file.Position=_offset;long remaining=file.Length-_offset;
            byte[] buffer=new byte[65536];using var pending=new MemoryStream();
            while(remaining>0) {
                int read=file.Read(buffer,0,(int)Math.Min(buffer.Length,remaining));if(read==0)break;remaining-=read;
                int from=0;
                for(int i=0;i<read;i++)if(buffer[i]==(byte)'\n') {
                    pending.Write(buffer,from,i-from);
                    Accept(Encoding.UTF8.GetString(pending.GetBuffer(),0,(int)pending.Length));
                    _offset+=pending.Length+1;pending.SetLength(0);from=i+1;
                }
                pending.Write(buffer,from,read-from);
            } // Incomplete final line is reread on the next poll, including UTF-8 fragments.
            return View(page);
        }catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException) {return null;}
    }
    private void Reset(string turn) {_turn=turn;_state="inProgress";_startedAt=0;_pages.Clear();_seen.Clear();_lastMessage=null;_hasReply=false;}
    internal void Accept(string line) {
        try {
            using var doc=JsonDocument.Parse(line);var root=doc.RootElement;
            if(!root.TryGetProperty("payload",out var p)||p.ValueKind!=JsonValueKind.Object)return;
            string S(JsonElement v,string key)=>v.TryGetProperty(key,out var x)&&x.ValueKind==JsonValueKind.String?x.GetString()!:"";
            string type=S(root,"type"),kind=S(p,"type");
            if(type=="event_msg"&&kind=="task_started") {
                string id=S(p,"turn_id");if(id.Length==0)return;
                _acceptTurn=_targetTurn is null||id==_targetTurn;
                if(!_acceptTurn)return;
                if(id!=_turn)Reset(id);
                if(DateTimeOffset.TryParse(S(root,"timestamp"),out var time))_startedAt=time.ToUnixTimeSeconds();
                return;
            }
            if(_turn.Length==0||!_acceptTurn)return;
            if(type=="event_msg") {
                string id=S(p,"turn_id");if(id.Length>0&&id!=_turn)return;
                if(kind=="turn_aborted"&&id==_turn) {_state="interrupted";return;}
                if(kind=="task_complete") {
                    if(id!=_turn)return;
                    _state="completed";string final=S(p,"last_agent_message");
                    if(!string.IsNullOrWhiteSpace(final))_hasReply=true;
                    if(!string.IsNullOrWhiteSpace(final)&&final!=_lastMessage)Add("答复",final);
                    return;
                }
                if(kind=="item_completed"&&p.TryGetProperty("item",out var item)) {
                    string itemType=S(item,"type"),itemId=S(item,"id");
                    if(itemType=="AgentMessage") {
                        if(S(item,"phase") is "commentary" or "final_answer")Message(item,itemId);
                    }
                }
            } else if(type=="response_item"&&kind=="message"&&S(p,"role")=="assistant"&&S(p,"phase") is "commentary" or "final_answer")
                Message(p,S(p,"id"));
        }catch(Exception ex) when(ex is JsonException or InvalidOperationException or FormatException) { }
    }
    private void Message(JsonElement item,string id) {
        if(id.Length>0&&_seen.Contains(id))return;
        if(!item.TryGetProperty("content",out var content))return;
        string message=content.ValueKind==JsonValueKind.String?content.GetString()!:
            content.ValueKind==JsonValueKind.Array?string.Join("\n",content.EnumerateArray().Where(x=>x.ValueKind==JsonValueKind.Object&&
                x.TryGetProperty("type",out var t)&&t.GetString() is "Text" or "text" or "output_text"&&x.TryGetProperty("text",out var v)&&v.ValueKind==JsonValueKind.String).Select(x=>x.GetProperty("text").GetString())):"";
        if(string.IsNullOrWhiteSpace(message)||message==_lastMessage)return;
        // An unsupported/empty item must not suppress a later readable copy.
        if(id.Length>0)_seen.Add(id);
        _lastMessage=message;
        if(item.TryGetProperty("phase",out var finalPhase)&&finalPhase.GetString()=="final_answer")_hasReply=true;
        Add(item.TryGetProperty("phase",out var phase)&&phase.GetString()=="final_answer"?"答复":"进度",message);
    }
    private void Add(string label,string message) {
        // Stable pages retain all public text for this turn. Each page fits the
        // existing status envelope; no extra embedded-device history buffer.
        string value=message.Trim()+"\n\n";
        foreach(var rune in value.EnumerateRunes()) {
            if(_pages.Count==0||Encoding.UTF8.GetByteCount(_pages[^1])+rune.Utf8SequenceLength>2200)_pages.Add("");
            _pages[^1]+=rune.ToString();
        }
    }
    internal static Tab5ActivityPage FromDesktop(Tab5CodexDesktop.State state,int page) {
        var activity=new Tab5CodexActivity();activity.Reset(state.TurnId);activity._state=state.Status;
        activity._startedAt=(long)Tab5CodexDesktop.Child(state.Turn,"turnStartedAtMs").TryGetDoubleSafe()/1000;
        var items=Tab5CodexDesktop.Child(state.Turn,"items");
        if(items.ValueKind==JsonValueKind.Array)foreach(var item in items.EnumerateArray()) {
            string type=Tab5CodexDesktop.Text(item,"type"),phase=Tab5CodexDesktop.Text(item,"phase");
            if(type=="agentMessage"&&phase is "commentary" or "final_answer") {
                string text=Tab5CodexDesktop.Text(item,"text");
                if(text.Length>0)activity.Add(phase=="final_answer"?"答复":"进度",text);
                if(phase=="final_answer"&&text.Length>0)activity._hasReply=true;
            }
        }
        return activity.View(page)??new(state.TurnId,state.Status,"暂未收到会话动态。",0,1,0,false);
    }
    internal bool HasText=>_pages.Count>0;
    internal Tab5ActivityPage? View(int page=-1) {
        if(_turn.Length==0)return null;
        int total=Math.Max(1,_pages.Count),selected=page<0?total-1:Math.Clamp(page,0,total-1);
        return new(_turn,_state,_pages.Count==0?(_state=="inProgress"?"Codex 正在处理，等待新的动态…":"本轮尚无公开文字。"): _pages[selected],selected,total,_startedAt,_hasReply,_pages.Count>0);
    }
}
