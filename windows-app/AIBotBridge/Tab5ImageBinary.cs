using System.Buffers.Binary;
namespace AIBotBridge;
internal static class Tab5ImageBinary
{
    internal static int MetadataLength(byte[] clear) {
        if(clear.Length<8||!clear.AsSpan(0,4).SequenceEqual("T5I1"u8))throw new ArgumentException("invalid_image_envelope");
        uint length=BinaryPrimitives.ReadUInt32LittleEndian(clear.AsSpan(4));
        if(length is <2 or >4096||clear.Length-8-length is <16 or >Tab5CodexImages.MaxBytes)throw new ArgumentException("invalid_image_envelope");
        return (int)length;
    }
}
