using System.Text;
using System.Text.Json;
namespace AIBotBridge;

// Only the selected main branch's public assistant text reaches the device.
internal static class ClaudeHistory
{
    internal sealed record Value(List<Dictionary<string,object>> Turns,bool Limited,HashSet<string> Accepted);
    private sealed record Node(string Id,string? Parent,bool Human,string Text,long At);
    internal static Value Read(string root,Guid session,byte[] key){
        var options=new EnumerationOptions{RecurseSubdirectories=true,IgnoreInaccessible=false,AttributesToSkip=FileAttributes.ReparsePoint};
        var matches=Directory.EnumerateFiles(root,"*.jsonl",options).Where(p=>!p.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Contains("subagents")&&Guid.TryParse(Path.GetFileNameWithoutExtension(p),out var id)&&id==session).Take(2).ToArray();
        if(matches.Length!=1)throw new IOException("Claude history unavailable");
        using var file=new FileStream(matches[0],FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        bool limited=file.Length>16*1024*1024;long offset=Math.Max(0,file.Length-16*1024*1024);file.Position=offset;
        byte[] bytes=new byte[(int)(file.Length-offset)];file.ReadExactly(bytes);
        var nodes=new Dictionary<string,Node>();string? leaf=null;int ordinal=0;
        var lines=Encoding.UTF8.GetString(bytes).Split('\n');
        foreach(string line in lines.Skip(limited?1:0).SkipLast(1)){
            if(Encoding.UTF8.GetByteCount(line)>2_000_000){limited=true;continue;}
            JsonDocument doc;try{doc=JsonDocument.Parse(line);}catch(JsonException){continue;}
            using(doc){var r=doc.RootElement;
                if(!Guid.TryParse(Text(r,"sessionId"),out var id)||id!=session||Text(r,"type") is not ("user" or "assistant")||r.TryGetProperty("isSidechain",out var side)&&side.ValueKind!=JsonValueKind.False)continue;
                string node=Guid.TryParse(Text(r,"uuid"),out var uuid)?uuid.ToString():"legacy-"+(++ordinal);
                if(nodes.ContainsKey(node))continue;
                string? parent=leaf;
                if(r.TryGetProperty("parentUuid",out var p)){
                    if(p.ValueKind==JsonValueKind.Null)parent=null;
                    else if(p.ValueKind==JsonValueKind.String&&Guid.TryParse(p.GetString(),out var value))parent=value.ToString();
                    else throw new IOException("Claude branch unavailable");
                }
                string text="";bool human=false,meta=r.TryGetProperty("isMeta",out var m)&&m.ValueKind!=JsonValueKind.False;
                if(!meta&&r.TryGetProperty("message",out var message)&&message.TryGetProperty("content",out var content)){
                    if(Text(r,"type")=="assistant"&&Text(message,"role")=="assistant"&&content.ValueKind==JsonValueKind.Array)
                        text=string.Join("\n",content.EnumerateArray().Where(c=>Text(c,"type")=="text").Select(c=>Text(c,"text")??""));
                    if(Text(r,"type")=="user"&&(!r.TryGetProperty("toolUseResult",out var tool)||tool.ValueKind==JsonValueKind.Null))
                        human=content.ValueKind==JsonValueKind.String||content.ValueKind==JsonValueKind.Array&&content.GetArrayLength()>0&&content.EnumerateArray().All(c=>Text(c,"type") is "text" or "image");
                }
                nodes[node]=new(node,parent,human,text,DateTimeOffset.TryParse(Text(r,"timestamp"),out var at)?at.ToUnixTimeSeconds():0);leaf=node;
            }
        }
        var branch=new List<Node>();var visited=new HashSet<string>();
        while(leaf is not null){if(!visited.Add(leaf))throw new IOException("Claude branch unavailable");if(!nodes.TryGetValue(leaf,out var node)){limited=true;break;}branch.Add(node);leaf=node.Parent;}
        if(branch.Count==0)throw new IOException("Claude history unavailable");branch.Reverse();
        var turns=new List<Dictionary<string,object>>();var items=new List<object>();Node? start=null;
        void Finish(){if(start is null)return;turns.Add(new(){["id"]=ClaudeCatalog.Alias("claude:turn:"+session+":"+start.Id,key),["startedAt"]=start.At,["status"]="unknown",["items"]=items.ToArray()});items.Clear();}
        int total=0;
        foreach(var node in branch){if(node.Human){Finish();start=node;}start??=node;
            if(node.Text.Length>0){int size=Encoding.UTF8.GetByteCount(node.Text);if(size>1024*1024||total+size>4*1024*1024)throw new IOException("Claude public text too large");total+=size;items.Add(new{type="agentMessage",phase="final_answer",text=node.Text});}
        }
        Finish();if(turns.Count>128){turns=turns.TakeLast(128).ToList();limited=true;}
        return new(turns,limited,branch.Where(n=>n.Human).Select(n=>n.Id).ToHashSet());
    }
    internal static string? Text(JsonElement value,string key)=>value.ValueKind==JsonValueKind.Object&&value.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
}
