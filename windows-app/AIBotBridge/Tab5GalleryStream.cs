using System.Buffers.Binary;
using System.Text.Json;

namespace AIBotBridge;

// One authenticated read, one continuous HTTP response. Each bounded block
// retains the existing AES-GCM/T5R2 envelope and binds its order and range.
// The receiver can durably save verified blocks before the response finishes.
internal static class Tab5GalleryStream
{
    internal const int MaximumImage=1048576;
    internal static byte[] Encode(Tab5RpcDataBody body,byte[] key,string nonce)
    {
        var metadata=JsonSerializer.SerializeToElement(body.Metadata,JsonDefaults.Options);
        int offset=metadata.GetProperty("offset").GetInt32(),count=body.Data.Length;
        if(offset<0||count is <1 or >MaximumImage||offset>MaximumImage-count)throw new ArgumentException("gallery_stream_range");
        int blocks=(count+Tab5RpcBinary.BulkData-1)/Tab5RpcBinary.BulkData;
        using var output=new MemoryStream();
        byte[] header=new byte[16];"T5S1"u8.CopyTo(header);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4),offset);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(8),count);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(12),blocks);output.Write(header);
        for(int at=0,block=0;at<count;at+=Tab5RpcBinary.BulkData,block++) {
            int size=Math.Min(Tab5RpcBinary.BulkData,count-at);
            byte[] meta=JsonSerializer.SerializeToUtf8Bytes(new{status=200,body=new{offset=offset+at,count=size,streamOffset=offset,streamBytes=count,block,blocks}},JsonDefaults.Options);
            byte[] packet=Tab5Protocol.Encrypt(key,nonce,Tab5RpcBinary.Pack(meta,body.Data.Span.Slice(at,size),bulk:true),Tab5RpcBinary.MailboxMaximum);
            output.Write(Tab5Protocol.WithLength(packet));
        }
        return output.ToArray();
    }
}
