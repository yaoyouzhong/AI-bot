using System.Text.Json;
using System.Security.Cryptography;

namespace AIBotBridge;
internal sealed record Tab5RpcDataBody(object Metadata,ReadOnlyMemory<byte> Data);
internal static class Tab5TransportBenchmark
{
    // Authenticated RAM-only traffic. Never touches a task, attachment or flash.
    internal static (int Status,object Body) Handle(JsonElement root,ReadOnlyMemory<byte> raw=default,bool binaryReply=false,bool bulkReply=false) {
        if(!root.TryGetProperty("offset",out var o)||!o.TryGetInt32(out int offset)||offset is <0 or >1048576||
           !root.TryGetProperty("count",out var c)||!c.TryGetInt32(out int count)||count<1||count>(bulkReply?Tab5RpcBinary.BulkData:8192))return(400,new{error="invalid_range"});
        var expected=Pattern(offset,count);
        bool legacy=root.TryGetProperty("data",out var d),upload=legacy||!raw.IsEmpty;
        if(!raw.IsEmpty&&(legacy||!raw.Span.SequenceEqual(expected))||legacy&&(d.ValueKind!=JsonValueKind.String||!Convert.FromBase64String(d.GetString()!).SequenceEqual(expected)))return(400,new{error="benchmark_bytes_mismatch"});
        string sha256=Convert.ToHexString(SHA256.HashData(expected)).ToLowerInvariant();
        if(!upload&&binaryReply)return(200,new Tab5RpcDataBody(new{offset,count,sha256},expected));
        return(200,new{offset,count,sha256,data=upload?null:Convert.ToBase64String(expected)});
    }
    internal static byte[] Pattern(int offset,int count) {
        var bytes=new byte[count];for(int i=0;i<count;i++)bytes[i]=(byte)((offset+i)*31+17);return bytes;
    }
}
