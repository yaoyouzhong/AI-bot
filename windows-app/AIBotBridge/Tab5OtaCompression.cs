using System.IO.Compression;
using System.Buffers.Binary;

namespace AIBotBridge;

internal static class Tab5OtaCompression
{
    internal const string StreamEncoding="aibot-zlib-blocks-v1";
    internal static byte[] Stream(byte[] image) {
        using var output=new MemoryStream();
        Span<byte> header=stackalloc byte[16];
        for(int offset=0;offset<image.Length;offset+=Tab5RpcBinary.BulkData) {
            int count=Math.Min(Tab5RpcBinary.BulkData,image.Length-offset);
            var block=Range(image.AsMemory(offset,count),offset,true);
            BinaryPrimitives.WriteInt32LittleEndian(header,offset);
            BinaryPrimitives.WriteInt32LittleEndian(header[4..],count);
            BinaryPrimitives.WriteInt32LittleEndian(header[8..],block.Data.Length);
            BinaryPrimitives.WriteInt32LittleEndian(header[12..],block.Data.Length<count?1:0);
            output.Write(header);output.Write(block.Data.Span);
        }
        return output.ToArray();
    }
    // Independent zlib streams retain range retries and the existing 48 KiB
    // bounds. Never compress ciphertext or enlarge incompressible ranges.
    internal static Tab5RpcDataBody Range(ReadOnlyMemory<byte> bytes,int offset,bool enabled) {
        if(bytes.Length is <1 or >Tab5RpcBinary.BulkData)throw new ArgumentOutOfRangeException(nameof(bytes));
        if(enabled) {
            using var output=new MemoryStream();
            using(var encoder=new ZLibStream(output,CompressionLevel.Optimal,leaveOpen:true))encoder.Write(bytes.Span);
            // Include room for additional metadata before choosing compression.
            if(output.Length+96<bytes.Length)return new(new {offset,encoding="zlib",decodedSize=bytes.Length},output.ToArray());
        }
        return new(new {offset},bytes);
    }
}
