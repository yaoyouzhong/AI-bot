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
            string turnId=id.GetString()!;var items=new JsonArray();var seen=new HashSet<string>();string? cursor=null;int bytes=0;
            if(replies)for(int batch=0;batch<1024;batch++) {
                var parameters=new{threadId=task,turnId,limit=16,sortDirection="asc",cursor};
                var itemPage=await call("thread/turns/items/list",parameters,token).WaitAsync(token);
                bool legacy=UnsupportedMethod(itemPage);
                if(legacy)itemPage=await call("thread/items/list",parameters,token).WaitAsync(token);
                foreach(var entry in Rows(itemPage,16).EnumerateArray()) {
                    var item=entry;
                    if(legacy||entry.TryGetProperty("item",out _)) {
                        if(!entry.TryGetProperty("turnId",out var owner)||owner.GetString()!=turnId||!entry.TryGetProperty("item",out item))throw new IOException("Item turn mismatch");
                    }
                    if(item.ValueKind!=JsonValueKind.Object||!item.TryGetProperty("type",out var type)||type.GetString()!="agentMessage")continue;
                    if(!item.TryGetProperty("text",out var text)||text.ValueKind!=JsonValueKind.String)continue;
                    string value=text.GetString()!;bytes+=Encoding.UTF8.GetByteCount(value);
                    if(bytes>1024*1024)throw new IOException("Public reply budget exceeded");
                    // Retain public text only, not raw tools, reasoning or metadata.
                    items.Add(new JsonObject{["type"]="agentMessage",["text"]=value});
                }
                var r=itemPage.GetProperty("result");
                if(!r.TryGetProperty("nextCursor",out var next)||next.ValueKind==JsonValueKind.Null)break;
                if(next.ValueKind!=JsonValueKind.String||(cursor=next.GetString()) is not {Length:>0 and <=4096}||!seen.Add(cursor)||batch==1023)
                    throw new IOException("Invalid history cursor");
            }
            var selected=new JsonObject{["id"]=turnId,["items"]=items};
            foreach(string key in new[]{"status","startedAt","completedAt"})if(turn.TryGetProperty(key,out var value))selected[key]=JsonNode.Parse(value.GetRawText());
            output.Add(selected);
        }
        var node=JsonNode.Parse(metadata.GetRawText())!;node["result"]!["thread"]!["turns"]=output;
        return JsonSerializer.SerializeToElement(node);
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
