using System.Text;
using System.Text.Json;
namespace AIBotBridge;

internal static class ClaudePublicRepliesSelfTest
{
    internal static void Run()
    {
        var root=Path.Combine(Path.GetTempPath(),"aibot-claude-public-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);
        try {
            var id=Guid.NewGuid();var path=Path.Combine(root,id+".jsonl");
            var text=string.Concat(Enumerable.Repeat("公开中文🙂 reply\n",200));
            string Row(string content)=>JsonSerializer.Serialize(new{type="assistant",sessionId=id,message=new{role="assistant",content=new object[]{new{type="thinking",thinking="private reasoning"},new{type="tool_use",input="private command"},new{type="text",text=content}}}})+"\n";
            File.WriteAllText(path,Row(text));bool enabled=true;var reader=new ClaudeReplyReader(()=>enabled,root);var now=DateTimeOffset.UtcNow;
            var binding=reader.Advertise(id,"会话 A",now)!;reader.Advertise(Guid.NewGuid(),"会话 B",now);
            (int Status,object Body) Read(int page,string revision="",string op="read"){
                using var request=JsonDocument.Parse(JsonSerializer.Serialize(new{op,binding,page,revision}));return reader.Handle(request.RootElement,now);
            }
            void Check(bool value,string name){if(!value)throw new InvalidOperationException("Claude public reply: "+name);}
            var first=Read(0);Check(first.Status==200,"read");using var doc=JsonDocument.Parse(JsonSerializer.Serialize(first.Body));
            int total=doc.RootElement.GetProperty("total").GetInt32();var rev=doc.RootElement.GetProperty("revision").GetString()!;
            Check(total>1&&doc.RootElement.GetProperty("title").GetString()=="会话 A","pinned source");var copy=new StringBuilder();
            for(int page=0;page<total;page++){
                var response=Read(page,rev);Check(response.Status==200,"page");var encoded=JsonSerializer.Serialize(response.Body);using var body=JsonDocument.Parse(encoded);
                var part=body.RootElement.GetProperty("text").GetString()!;Check(Encoding.UTF8.GetByteCount(part)<=1600,"bounded UTF8");copy.Append(part);
                Check(!encoded.Contains("private")&&!encoded.Contains(id.ToString()),"privacy");
            }
            Check(copy.ToString()==text.Trim(),"lossless pages");File.WriteAllText(path,Row("new public reply"));Check(Read(0,rev).Status==409,"revision changes");
            Check(Read(0,op:"send").Status==400,"read only");enabled=false;Check(Read(0).Status==403,"source stopped");enabled=true;Check(Read(0).Status==409,"binding invalidated");
            var user=JsonSerializer.Serialize(new{type="user",sessionId=id,message=new{role="user",content="private"}})+"\n";
            File.WriteAllText(path,user+JsonSerializer.Serialize(new{type="assistant",sessionId=id,message=new{role="assistant",content="unknown format"}})+"\n");
            binding=reader.Advertise(id,"会话 A",now)!;
            Check(Read(-1).Status==503,"unknown reply is not empty success");
            File.WriteAllText(path,user);Check(Read(-1).Status==200,"no reply yet is valid empty");
            Console.WriteLine("CLAUDE_PUBLIC_REPLIES_OK privacy, binding, pages, source stop");
        }finally{Directory.Delete(root,true);}
    }
}
