using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
namespace AIBotBridge;

// Only the selected main branch's public assistant text reaches the device.
internal static class ClaudeHistory
{
    internal sealed record Value(List<Dictionary<string,object>> Turns,bool Limited,HashSet<string> Accepted,Dictionary<string,string> InputDigests);
    private sealed record Node(string Id,string? Parent,bool Human,string Text,long At,string? InputDigest,bool Assistant,bool Ended,Guid? Prompt,bool StopNotice,string? ReferencePrompt,ClaudeBackgroundApi.FileImage? FileImage);
    internal static Value Read(string root,Guid session,byte[] key,Func<bool>? verifiedNewPanel=null,bool backgroundImages=false){
        var options=new EnumerationOptions{RecurseSubdirectories=true,IgnoreInaccessible=false,AttributesToSkip=FileAttributes.ReparsePoint};
        var matches=Directory.EnumerateFiles(root,"*.jsonl",options).Where(p=>!p.Split(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar).Contains("subagents")&&Guid.TryParse(Path.GetFileNameWithoutExtension(p),out var id)&&id==session).Take(2).ToArray();
        if(matches.Length==0&&verifiedNewPanel?.Invoke()==true)return new([],false,[],[]);
        if(matches.Length!=1)throw new IOException("Claude history unavailable");
        using var file=new FileStream(matches[0],FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        bool limited=file.Length>16*1024*1024;long offset=Math.Max(0,file.Length-16*1024*1024);file.Position=offset;
        byte[] bytes=new byte[(int)(file.Length-offset)];file.ReadExactly(bytes);
        bool titleOnly=!limited&&bytes.Length>0&&bytes[^1]==(byte)'\n';
        var nodes=new Dictionary<string,Node>();string? leaf=null;int ordinal=0;
        var lines=Encoding.UTF8.GetString(bytes).Split('\n');
        foreach(string line in lines.Skip(limited?1:0).SkipLast(1)){
            if(Encoding.UTF8.GetByteCount(line)>10_000_000){limited=true;titleOnly=false;continue;}
            JsonDocument doc;try{doc=JsonDocument.Parse(line);}catch(JsonException){titleOnly=false;continue;}
            using(doc){var r=doc.RootElement;
                string type=Text(r,"type")??"";
                if(Text(r,"sessionId")!=session.ToString()||
                    !(type=="custom-title"&&Text(r,"customTitle") is {Length:>0}||type=="ai-title"&&Text(r,"aiTitle") is {Length:>0}))titleOnly=false;
                bool link=type=="attachment"||type=="system"&&Text(r,"subtype") is "turn_duration" or "away_summary" or "informational" or "api_error";
                if(!Guid.TryParse(Text(r,"sessionId"),out var id)||id!=session||(!link&&type is not ("user" or "assistant"))||r.TryGetProperty("isSidechain",out var side)&&side.ValueKind!=JsonValueKind.False)continue;
                // Native context attachments link the public message graph, but
                // are never public text, user receipts, or a new selected leaf.
                if(link&&(!Guid.TryParse(Text(r,"uuid"),out _)||!r.TryGetProperty("parentUuid",out _)))continue;
                string node=Guid.TryParse(Text(r,"uuid"),out var uuid)?uuid.ToString():"legacy-"+(++ordinal);
                if(nodes.ContainsKey(node))continue;
                string? parent=leaf;
                if(r.TryGetProperty("parentUuid",out var p)){
                    if(p.ValueKind==JsonValueKind.Null)parent=null;
                    else if(p.ValueKind==JsonValueKind.String&&Guid.TryParse(p.GetString(),out var value))parent=value.ToString();
                    else throw new IOException("Claude branch unavailable");
                }
                string text="";string? inputDigest=null,referencePrompt=null;bool human=false,assistant=false,ended=false,stopNotice=false,meta=r.TryGetProperty("isMeta",out var m)&&m.ValueKind!=JsonValueKind.False;
                if(!link&&!meta&&r.TryGetProperty("message",out var message)&&message.TryGetProperty("content",out var content)){
                    if(Text(r,"type")=="assistant"&&Text(message,"role")=="assistant"&&content.ValueKind==JsonValueKind.Array)
                        {assistant=true;text=string.Join("\n",content.EnumerateArray().Where(c=>Text(c,"type")=="text").Select(c=>Text(c,"text")??""));ended=Text(message,"stop_reason")=="end_turn"&&text.Length>0;}
                    if(type=="user"&&(!r.TryGetProperty("origin",out var origin)||Text(origin,"kind")!="peer")&&(!r.TryGetProperty("toolUseResult",out var tool)||tool.ValueKind==JsonValueKind.Null))
                        human=content.ValueKind==JsonValueKind.String||content.ValueKind==JsonValueKind.Array&&content.GetArrayLength()>0&&content.EnumerateArray().All(c=>Text(c,"type") is "text" or "image");
                    if(human){
                        inputDigest=ClaudeNativeContent.MessageDigest(content);
                        if(backgroundImages)referencePrompt=ClaudeBackgroundApi.ReferencePrompt(content);
                        stopNotice=content.ValueKind==JsonValueKind.Array&&content.GetArrayLength()==1&&Text(content[0],"type")=="text"&&
                            Text(content[0],"text") is "[Request interrupted by user]" or "[Request interrupted by user for tool use]";
                    }
                }
                Guid? prompt=Guid.TryParse(Text(r,"promptId"),out var group)&&group!=Guid.Empty?group:null;
                var fileImage=backgroundImages&&type=="attachment"?ClaudeBackgroundApi.AttachedImage(r):null;
                nodes[node]=new(node,parent,human,text,DateTimeOffset.TryParse(Text(r,"timestamp"),out var at)?at.ToUnixTimeSeconds():0,inputDigest,assistant,ended,prompt,stopNotice,referencePrompt,fileImage);if(!link)leaf=node;
            }
        }
        var branch=new List<Node>();var visited=new HashSet<string>();
        while(leaf is not null){if(!visited.Add(leaf))throw new IOException("Claude branch unavailable");if(!nodes.TryGetValue(leaf,out var node)){limited=true;break;}branch.Add(node);leaf=node.Parent;}
        if(branch.Count==0){
            if(titleOnly&&verifiedNewPanel?.Invoke()==true)return new([],false,[],[]);
            throw new IOException("Claude history unavailable");
        }
        branch.Reverse();
        var turns=new List<Dictionary<string,object>>();var items=new List<object>();Node? start=null;bool complete=false,interrupted=false;long endedAt=0;
        var accepted=new HashSet<string>();var digests=new Dictionary<string,string>();
        var files=new List<ClaudeBackgroundApi.FileImage>();bool sawAssistant=false;
        void Finish(){if(start is null)return;
            if(ClaudeBackgroundApi.ReferenceDigest(start.ReferencePrompt,files) is {} digest)digests[start.Id]=digest;
            files.Clear();sawAssistant=false;
            turns.Add(new(){["id"]=ClaudeCatalog.Alias("claude:turn:"+session+":"+start.Id,key),["startedAt"]=start.At,["status"]=interrupted?"interrupted":complete?"completed":"unknown",["endedAt"]=endedAt,["items"]=items.ToArray()});items.Clear();}
        int total=0;
        foreach(var node in branch){
            // Claude writes this notice as a user row within the original prompt.
            // The same literal submitted under a new prompt ID is a real input.
            if(node.StopNotice&&node.Prompt.HasValue&&start is {Human:true}&&node.Prompt==start.Prompt){interrupted=true;endedAt=node.At;continue;}
            if(node.Human){Finish();start=node;complete=false;interrupted=false;endedAt=0;accepted.Add(node.Id);if(node.InputDigest!=null)digests[node.Id]=node.InputDigest;}start??=node;
            if(!sawAssistant&&start.ReferencePrompt is not null&&node.FileImage is {} image&&files.Count<=Tab5CodexImages.MaxCount)files.Add(image);
            if(node.Assistant){sawAssistant=true;complete=node.Ended;endedAt=complete?node.At:0;}
            if(node.Text.Length>0){int size=Encoding.UTF8.GetByteCount(node.Text);if(size>1024*1024||total+size>4*1024*1024)throw new IOException("Claude public text too large");total+=size;items.Add(new{type="agentMessage",phase="final_answer",text=node.Text});}
        }
        Finish();if(turns.Count>128){turns=turns.TakeLast(128).ToList();limited=true;}
        return new(turns,limited,accepted,digests);
    }
    internal static string? Text(JsonElement value,string key)=>value.ValueKind==JsonValueKind.Object&&value.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
}
