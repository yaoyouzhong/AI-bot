using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AIBotBridge;
// Inside the existing HMAC/AES-GCM envelope. Only authenticated peers opt in;
// old peers retain JSON. Metadata is bounded and cannot also carry payload data.
internal static class Tab5RpcBinary
{
    internal const int BulkData=49152, MailboxMaximum=65535, RequestMaximum=MailboxMaximum-96;
    internal static (JsonDocument Metadata,ReadOnlyMemory<byte> Data) Open(byte[] clear,bool response=false) {
        bool bulk=clear.Length>=4&&clear.AsSpan(0,4).SequenceEqual("T5R2"u8);
        if(!bulk&&(clear.Length<4||!clear.AsSpan(0,4).SequenceEqual("T5R1"u8)))return(JsonDocument.Parse(clear),default);
        if(clear.Length<9)throw new ArgumentException("rpc_binary_truncated");
        uint n=BinaryPrimitives.ReadUInt32LittleEndian(clear.AsSpan(4));
        if(n is <2 or >4096||n>clear.Length-8)throw new ArgumentException("rpc_binary_metadata");
        int count=clear.Length-8-(int)n;
        if(count<1||count>(bulk?BulkData:response?16384:8192))throw new ArgumentException("rpc_binary_payload");
        var doc=JsonDocument.Parse(clear.AsMemory(8,(int)n));
        try {
            var target=response?doc.RootElement.GetProperty("body"):doc.RootElement;
            if(target.ValueKind!=JsonValueKind.Object||target.TryGetProperty("data",out _))throw new ArgumentException("rpc_binary_duplicate_data");
            return(doc,clear.AsMemory(8+(int)n,count));
        }catch{doc.Dispose();throw;}
    }
    internal static byte[] Pack(byte[] metadata,ReadOnlySpan<byte> data,bool response=true,bool bulk=false) {
        if(metadata.Length is <2 or >4096||data.Length<1||data.Length>(bulk?BulkData:response?16384:8192))throw new ArgumentException("rpc_binary_bounds");
        byte[] wire=new byte[8+metadata.Length+data.Length];(bulk?"T5R2"u8:"T5R1"u8).CopyTo(wire);
        BinaryPrimitives.WriteUInt32LittleEndian(wire.AsSpan(4),(uint)metadata.Length);
        metadata.CopyTo(wire,8);data.CopyTo(wire.AsSpan(8+metadata.Length));return wire;
    }
    internal static byte[] Decode(byte[] clear,bool response=false) {
        var opened=Open(clear,response);using var doc=opened.Metadata;
        if(opened.Data.IsEmpty)return clear;
        var root=JsonNode.Parse(doc.RootElement.GetRawText())!.AsObject();
        var target=response?root["body"]!.AsObject():root;
        target["data"]=Convert.ToBase64String(opened.Data.Span);
        return JsonSerializer.SerializeToUtf8Bytes(root,JsonDefaults.Options);
    }
    internal static byte[] Encode(byte[] json,bool response=true) {
        var root=JsonNode.Parse(json) as JsonObject;
        var target=response?root?["body"] as JsonObject:root;
        if(target?["data"] is not JsonValue value||!value.TryGetValue<string>(out var text)||string.IsNullOrEmpty(text))return json;
        byte[] data=Convert.FromBase64String(text);
        if(data.Length> (response?16384:8192))return json;
        target.Remove("data");byte[] metadata=JsonSerializer.SerializeToUtf8Bytes(root,JsonDefaults.Options);
        if(metadata.Length>4096)return json;
        byte[] wire=new byte[8+metadata.Length+data.Length];"T5R1"u8.CopyTo(wire);BinaryPrimitives.WriteUInt32LittleEndian(wire.AsSpan(4),(uint)metadata.Length);
        metadata.CopyTo(wire,8);data.CopyTo(wire,8+metadata.Length);return wire;
    }
}
