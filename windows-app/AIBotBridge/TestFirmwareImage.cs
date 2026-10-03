using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace AIBotBridge;
// Synthetic executable-format fixture. Contains no executable program or device data.
internal static class TestFirmwareImage
{
    internal static byte[] Create(string version,int length=1024) {
        if(length<1024||length%16!=0)throw new ArgumentOutOfRangeException(nameof(length));
        byte[] bytes=new byte[length];RandomNumberGenerator.Fill(bytes.AsSpan(32,length-80));
        bytes[0]=0xe9;bytes[1]=1;bytes[12]=18;bytes[23]=1;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(28),(uint)(length-80));
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(32),0xabcd5432);Array.Clear(bytes,48,64);
        Encoding.ASCII.GetBytes(version).CopyTo(bytes,48);Encoding.ASCII.GetBytes("aibot_tab5").CopyTo(bytes,80);
        byte checksum=0xef;foreach(byte b in bytes.AsSpan(32,length-80))checksum^=b;
        bytes[length-33]=checksum;SHA256.HashData(bytes.AsSpan(0,length-32)).CopyTo(bytes,length-32);return bytes;
    }
}
