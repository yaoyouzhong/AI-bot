using System.Buffers.Binary;
using System.Text;

namespace AIBotBridge;

// One mailbox RPC, serialized with status traffic on the existing GATT session.
// Every request keeps VoiceAsync's nonce, HMAC, AES-GCM, session and time checks.
internal sealed class Tab5BleVoice(
    Func<CancellationToken,Task<byte[]>> read,
    Func<byte[],CancellationToken,Task> write,
    Func<string,string,byte[],CancellationToken,Task<(int Status,byte[]? Packet)>> handle,
    int mtu)
{
    private uint _completed;
    internal long LastActive {get;private set;}
    internal async Task<bool> PumpAsync(CancellationToken token)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(12));
        token=deadline.Token;
        var part=await read(token);
        if(part.Length<8)throw new IOException("BLE voice header truncated");
        uint id=BinaryPrimitives.ReadUInt32LittleEndian(part);
        if(id==0)return false;
        LastActive=Environment.TickCount64;
        if(id==_completed)return true;
        int total=BinaryPrimitives.ReadUInt16LittleEndian(part.AsSpan(6));
        if(total is <124 or >12288)throw new IOException("BLE voice length invalid");
        var request=new byte[total];int offset=0;
        while(true) {
            if(part.Length<=8||part.Length>488||BinaryPrimitives.ReadUInt32LittleEndian(part)!=id||
                BinaryPrimitives.ReadUInt16LittleEndian(part.AsSpan(4))!=offset||
                BinaryPrimitives.ReadUInt16LittleEndian(part.AsSpan(6))!=total||offset+part.Length-8>total)
                throw new IOException("BLE voice fragments out of order");
            part.AsSpan(8).CopyTo(request.AsSpan(offset));offset+=part.Length-8;
            if(offset==total)break;
            var ack=new byte[7];ack[0]=1;BinaryPrimitives.WriteUInt32LittleEndian(ack.AsSpan(1),id);
            BinaryPrimitives.WriteUInt16LittleEndian(ack.AsSpan(5),(ushort)offset);
            await write(ack,token);part=await read(token);
        }
        var (status,response)=await handle(Encoding.ASCII.GetString(request,0,32),Encoding.ASCII.GetString(request,32,64),request[96..],token);
        if(status!=200||response is null||response.Length is <28 or >12288)throw new IOException("BLE voice authentication failed");
        int chunk=Math.Clamp(mtu-3,20,244)-9;
        for(offset=0;offset<response.Length;offset+=chunk) {
            int count=Math.Min(chunk,response.Length-offset);var packet=new byte[9+count];packet[0]=2;
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(1),id);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(5),(ushort)offset);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(7),(ushort)response.Length);
            response.AsSpan(offset,count).CopyTo(packet.AsSpan(9));await write(packet,token);
        }
        _completed=id;return true;
    }
}
