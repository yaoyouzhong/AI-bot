using System.Buffers.Binary;
using System.Text;

namespace AIBotBridge;

// One mailbox RPC, serialized with status traffic on the existing GATT session.
// Every request keeps VoiceAsync's nonce, HMAC, AES-GCM, session and time checks.
internal sealed class Tab5BleVoice(
    Func<CancellationToken,Task<byte[]>> read,
    Func<byte[],CancellationToken,Task> write,
    Func<string,string,byte[],CancellationToken,Task<(int Status,byte[]? Packet)>> handle,
    int mtu, int maximumResponse=12288, int deadlineSeconds=12, int fragmentSize=480, int responseChunk=0,string transportMode="read/ack",int maximumRequest=12288,Func<bool>? bulkPriority=null)
{
    private uint _completed;
    internal long LastActive {get;private set;}
    internal uint CompletedCount {get;private set;}
    internal string Progress {get;private set;}="idle";
    internal string Timing {get;private set;}="pending";
    private int _bulkActive;
    internal bool BulkActive=>Volatile.Read(ref _bulkActive)!=0;
    internal async Task<bool> PumpAsync(CancellationToken token)
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(token);deadline.CancelAfter(TimeSpan.FromSeconds(deadlineSeconds));
        token=deadline.Token;
        long started=Environment.TickCount64;
        Progress="read mailbox";
        var part=await read(token);
        if(part.Length<8)throw new IOException("BLE voice header truncated");
        uint id=BinaryPrimitives.ReadUInt32LittleEndian(part);
        if(id==0)return false;
        LastActive=Environment.TickCount64;
        if(id==_completed)return true;
        int total=BinaryPrimitives.ReadUInt16LittleEndian(part.AsSpan(6));
        if(total<124||total>maximumRequest)throw new IOException("BLE voice length invalid");
        var request=new byte[total];int offset=0;
        Volatile.Write(ref _bulkActive,total>12288||bulkPriority?.Invoke()==true?1:0);
        try {
        while(true) {
            Progress=$"read request {offset}/{total}";
            if(part.Length<=8||part.Length>fragmentSize+8||BinaryPrimitives.ReadUInt32LittleEndian(part)!=id||
                BinaryPrimitives.ReadUInt16LittleEndian(part.AsSpan(4))!=offset||
                BinaryPrimitives.ReadUInt16LittleEndian(part.AsSpan(6))!=total||offset+part.Length-8>total)
                throw new IOException("BLE voice fragments out of order");
            part.AsSpan(8).CopyTo(request.AsSpan(offset));offset+=part.Length-8;
            if(offset==total)break;
            var ack=new byte[7];ack[0]=1;BinaryPrimitives.WriteUInt32LittleEndian(ack.AsSpan(1),id);
            BinaryPrimitives.WriteUInt16LittleEndian(ack.AsSpan(5),(ushort)offset);
            await write(ack,token);part=await read(token);
        }
        Progress="handle authenticated request";
        long handling=Environment.TickCount64;
        var (status,response)=await handle(Encoding.ASCII.GetString(request,0,32),Encoding.ASCII.GetString(request,32,64),request[96..],token);
        long writing=Environment.TickCount64;
        if(status!=200||response is null||(response.Length<28||response.Length>maximumResponse))throw new IOException($"RPC response rejected: status={status}, bytes={response?.Length??0}");
        int chunk=responseChunk>0?responseChunk:Math.Clamp(mtu-3,20,244)-9;
        for(offset=0;offset<response.Length;offset+=chunk) {
            Progress=$"write response {offset}/{response.Length}";
            int count=Math.Min(chunk,response.Length-offset);var packet=new byte[9+count];packet[0]=2;
            BinaryPrimitives.WriteUInt32LittleEndian(packet.AsSpan(1),id);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(5),(ushort)offset);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(7),(ushort)response.Length);
            response.AsSpan(offset,count).CopyTo(packet.AsSpan(9));await write(packet,token);
        }
        _completed=id;CompletedCount++;Progress=$"response accepted {response.Length}/{response.Length}";
        LastActive=Environment.TickCount64;
        Timing=$"request={handling-started}ms; handler={writing-handling}ms; response={Environment.TickCount64-writing}ms; requestBytes={total}; bytes={response.Length}; mtu={mtu}; flow={transportMode}";return true;
        } finally {Volatile.Write(ref _bulkActive,0);}
    }
}
