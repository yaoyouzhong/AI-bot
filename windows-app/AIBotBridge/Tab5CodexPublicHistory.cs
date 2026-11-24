using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIBotBridge;

internal static class Tab5CodexPublicHistory
{
    internal delegate Task<JsonElement> Call(string method,object parameters,CancellationToken token);
    internal static async Task<JsonElement> ReadAsync(string task,bool replies,Call call,CancellationToken token)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(8));
        token=deadline.Token;
        var metadata=await call("thread/read",new{threadId=task,includeTurns=false},token).WaitAsync(token);
        return await PopulateAsync(metadata,task,replies,call,token);
    }
    internal static async Task<JsonElement> PopulateAsync(JsonElement metadata,string task,bool replies,Call call,CancellationToken token)
    {
        if(metadata.ValueKind!=JsonValueKind.Object||!metadata.TryGetProperty("result",out var result)||
            !result.TryGetProperty("thread",out var thread)||thread.ValueKind!=JsonValueKind.Object)return metadata;
        if(thread.TryGetProperty("id",out var identity)&&identity.GetString()!=task)throw new IOException("Thread identity mismatch");
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(8));token=deadline.Token;
        var page=await call("thread/turns/list",new{threadId=task,limit=1,sortDirection="desc",itemsView="notLoaded"},token).WaitAsync(token);
        var turns=Rows(page,1);var output=new JsonArray();
        if(turns.GetArrayLength()>0) {
            var turn=turns[0];
            if(!turn.TryGetProperty("id",out var id)||id.ValueKind!=JsonValueKind.String||string.IsNullOrWhiteSpace(id.GetString()))throw new IOException("Missing turn identity");
            string turnId=id.GetString()!;
            var items=replies?await ReadItemsAsync(task,turnId,call,token):new JsonArray();
            var selected=new JsonObject{["id"]=turnId,["items"]=items};
            foreach(string key in new[]{"status","startedAt","completedAt"})if(turn.TryGetProperty(key,out var value))selected[key]=JsonNode.Parse(value.GetRawText());
            output.Add(selected);
        }
        var node=JsonNode.Parse(metadata.GetRawText())!;node["result"]!["thread"]!["turns"]=output;
        return JsonSerializer.SerializeToElement(node);
    }

    internal static async Task<Tab5CodexDesktop.State> ReadPageAsync(string task,string selected,int offset,Call call,CancellationToken token) {
        var metadata=await call("thread/read",new{threadId=task,includeTurns=false},token).WaitAsync(token);
        var thread=Tab5CodexDesktop.Child(Tab5CodexDesktop.Child(metadata,"result"),"thread");
        if(Tab5CodexDesktop.Text(thread,"id")!=task)throw new IOException("Thread identity mismatch");

        var turns=new List<JsonElement>();var ids=new HashSet<string>();var seen=new HashSet<string>();
        string? cursor=null;int index=-1;
        for(int batch=0;batch<8;batch++) {
            var response=await call("thread/turns/list",new{threadId=task,limit=32,sortDirection="desc",itemsView="notLoaded",cursor},token).WaitAsync(token);
            foreach(var turn in Rows(response,32).EnumerateArray()) {
                string id=Tab5CodexDesktop.Text(turn,"id");
                if(!Guid.TryParse(id,out _)||!ids.Add(id))throw new IOException("Invalid reply turn directory");
                if(id==selected)index=turns.Count;
                turns.Add(turn);
            }
            cursor=NextCursor(response,seen);
            if(selected.Length==0)index=0;
            if(cursor is null||index>=0&&index-offset+1<turns.Count)break;
        }
        if(index<0||turns.Count==0||cursor is not null&&index-offset>=turns.Count)throw new IOException("Requested turn unavailable");
        index=Math.Clamp(index-offset,0,turns.Count-1);
        string Id(int i)=>Tab5CodexDesktop.Text(turns[i],"id");
        var chosen=turns[index];string turnId=Id(index);
        var items=await ReadItemsAsync(task,turnId,call,token,useDocumentedMethod:true);
        if(items.Count==0)throw new IOException("No public App Server reply");
        // A separate App Server reads storage, with no ownership of the desktop
        // turn. An unfinished rollout may be reported as interrupted.
        string status=Tab5CodexDesktop.Text(chosen,"status");
        if(status is not ("completed" or "interrupted" or "failed")||
           status=="interrupted"&&Tab5CodexDesktop.Child(chosen,"completedAt").ValueKind!=JsonValueKind.Number)status="stored";
        long started=(long)Tab5CodexDesktop.Child(chosen,"startedAt").TryGetDoubleSafe();
        var result=JsonSerializer.SerializeToElement(new{turnId,status,turnStartedAtMs=started*1000,items});
        return new("notLoaded","",result,false) {
            IsHistory=index!=0,HasPrevious=index+1<turns.Count||cursor is not null,HasNext=index>0,
            PreviousTurnId=index+1<turns.Count?Id(index+1):"",NextTurnId=index>0?Id(index-1):""
        };
    }

    private static async Task<JsonArray> ReadItemsAsync(string task,string turnId,Call call,CancellationToken token,bool useDocumentedMethod=false) {
        var items=new JsonArray();var seen=new HashSet<string>();string? cursor=null;int bytes=0;
        for(int batch=0;batch<1024;batch++) {
            var parameters=new{threadId=task,turnId,limit=16,sortDirection="asc",cursor};
            string method=useDocumentedMethod?"thread/items/list":"thread/turns/items/list";
            var response=await call(method,parameters,token).WaitAsync(token);
            if(!useDocumentedMethod&&UnsupportedMethod(response)) {
                useDocumentedMethod=true;
                response=await call("thread/items/list",parameters,token).WaitAsync(token);
            }
            foreach(var entry in Rows(response,16).EnumerateArray()) {
                var item=entry;
                if(entry.ValueKind!=JsonValueKind.Object)throw new IOException("Invalid history item");
                if(useDocumentedMethod||entry.TryGetProperty("item",out _)) {
                    if(Tab5CodexDesktop.Text(entry,"turnId")!=turnId||!entry.TryGetProperty("item",out item))throw new IOException("Item turn mismatch");
                }
                if(Tab5CodexDesktop.Text(item,"type")!="agentMessage")continue;
                string value=Tab5CodexDesktop.Text(item,"text"),phase=Tab5CodexDesktop.Text(item,"phase");
                if(string.IsNullOrWhiteSpace(value)||phase is not ("" or "commentary" or "final_answer"))continue;
                bytes+=Encoding.UTF8.GetByteCount(value);
                if(bytes>1024*1024)throw new IOException("Public reply budget exceeded");
                items.Add(new JsonObject{["type"]="agentMessage",["text"]=value,["phase"]=phase.Length==0?"final_answer":phase});
            }
            cursor=NextCursor(response,seen);
            if(cursor is null)return items;
        }
        throw new IOException("History item limit exceeded");
    }

    private static string? NextCursor(JsonElement response,HashSet<string> seen) {
        var result=response.GetProperty("result");
        if(!result.TryGetProperty("nextCursor",out var next)||next.ValueKind==JsonValueKind.Null)return null;
        if(next.ValueKind!=JsonValueKind.String||next.GetString() is not {Length:>0 and <=4096} cursor||!seen.Add(cursor))
            throw new IOException("Invalid history cursor");
        return cursor;
    }
    private static bool UnsupportedMethod(JsonElement response) =>
        response.TryGetProperty("error",out var error)&&error.TryGetProperty("code",out var code)&&
        (code.GetInt32()==-32601||(code.GetInt32()==-32600&&error.TryGetProperty("message",out var message)&&
            message.GetString()?.Contains("unknown variant `thread/turns/items/list`",StringComparison.Ordinal)==true));
    private static JsonElement Rows(JsonElement response,int limit) {
        if(Encoding.UTF8.GetByteCount(response.GetRawText())>8*1024*1024||!response.TryGetProperty("result",out var r)||
            !r.TryGetProperty("data",out var rows)||rows.ValueKind!=JsonValueKind.Array||rows.GetArrayLength()>limit)
            throw new IOException("Bounded history API unavailable");
        return rows;
    }
}

internal sealed class Tab5BoundedLines(TextReader reader,int maximum=8*1024*1024)
{
    private readonly char[] _buffer=new char[8192];private int _at,_count;
    internal async Task<string?> ReadAsync(CancellationToken token=default) {
        var line=new StringBuilder();
        while(true) {
            if(_at==_count){_count=await reader.ReadAsync(_buffer.AsMemory(),token);_at=0;if(_count==0)return line.Length==0?null:Finish(line);}
            int end=Array.IndexOf(_buffer,'\n',_at,_count-_at);int count=(end<0?_count:end)-_at;
            if(line.Length+count>maximum)throw new IOException("Codex response line exceeds limit");
            line.Append(_buffer,_at,count);_at+=count;
            if(end>=0){_at++;return Finish(line);}
        }
    }
    private string Finish(StringBuilder line) {
        string value=line.ToString().TrimEnd('\r');
        if(Encoding.UTF8.GetByteCount(value)>maximum)throw new IOException("Codex response exceeds byte limit");
        return value;
    }
}
