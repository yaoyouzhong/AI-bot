using System.Text;
using System.Text.Json;
namespace AIBotBridge;

internal static class ClaudePublicReplies
{
    internal sealed record Value(string Text,bool HasEarlier);
    internal static Value Read(string path,Guid sessionId)
    {
        if((File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)throw new IOException("unsupported session file");
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete);
        const int tail=16*1024*1024,maximum=65536;
        bool earlier=stream.Length>tail,known=false,unsupportedReply=false;
        if(earlier){stream.Seek(-tail,SeekOrigin.End);while(stream.ReadByte() is var b&&b is not (-1 or 10)){} }
        using var reader=new StreamReader(stream,Encoding.UTF8,true);
        var parts=new Queue<string>();var seen=new HashSet<string>();int bytes=0;
        foreach(var line in ClaudeSessionIndex.CompleteLines(reader)) {
            try {
                using var doc=JsonDocument.Parse(line);var e=doc.RootElement;
                if(!Guid.TryParse(Text(e,"sessionId"),out var sid)||sid!=sessionId||Excluded(e,"isSidechain")||Excluded(e,"isMeta")||Text(e,"type") is not ("user" or "assistant"))continue;
                if(Text(e,"type")=="user"){known=true;continue;}
                if(!e.TryGetProperty("message",out var m)||Text(m,"role")!="assistant"||!m.TryGetProperty("content",out var content)||content.ValueKind!=JsonValueKind.Array){unsupportedReply=true;continue;}
                known=true;
                if(Text(e,"uuid") is {} id){if(seen.Contains(id))continue;if(seen.Count<4096)seen.Add(id);}
                var text=string.Join("\n",content.EnumerateArray().Where(b=>Text(b,"type")=="text").Select(b=>Text(b,"text")).Where(t=>t is not null)).Trim();
                if(text.Length==0)continue;
                var encoded=Encoding.UTF8.GetBytes(text);
                if(encoded.Length>maximum){int start=encoded.Length-maximum;while(start<encoded.Length&&(encoded[start]&0xC0)==0x80)start++;text=Encoding.UTF8.GetString(encoded.AsSpan(start));earlier=true;}
                parts.Enqueue(text);bytes+=Encoding.UTF8.GetByteCount(text);
                while(parts.Count>64||bytes>maximum){bytes-=Encoding.UTF8.GetByteCount(parts.Dequeue());earlier=true;}
            }catch(JsonException){}
        }
        if(!known||(unsupportedReply&&parts.Count==0))throw new IOException("session unavailable");
        return new(string.Join("\n\n",parts),earlier);
    }
    private static string? Text(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var v)&&v.ValueKind==JsonValueKind.String?v.GetString():null;
    private static bool Excluded(JsonElement e,string key)=>e.ValueKind==JsonValueKind.Object&&e.TryGetProperty(key,out var v)&&v.ValueKind!=JsonValueKind.False;
}
