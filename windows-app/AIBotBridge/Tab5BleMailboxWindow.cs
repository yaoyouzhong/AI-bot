using System.Buffers.Binary;
using System.Threading.Channels;

namespace AIBotBridge;

// Opt-in extension on the same authenticated RPC/voice mailbox. The receiver
// still validates every id/offset/length before invoking its authenticated handler.
internal sealed class Tab5BleMailboxWindow(
    Func<CancellationToken,Task<byte[]>> read,
    Func<byte[],bool,CancellationToken,Task> write,
    bool notifications,bool unacknowledged,int mtu,int credits=4,
    Func<IReadOnlyList<byte[]>,CancellationToken,Task>? writeBatch=null,int notifyCredits=0)
{
    private readonly List<byte[]> _pendingWrites=[];
    private readonly int _credits=Math.Clamp(credits,1,32);
    private readonly int _notifyCredits=Math.Clamp(notifyCredits>0?notifyCredits:credits,1,32);
    private readonly Channel<byte[]> _packets=Channel.CreateBounded<byte[]>(new BoundedChannelOptions(Math.Clamp(notifyCredits>0?notifyCredits:credits,1,32)) {
        FullMode=BoundedChannelFullMode.Wait,SingleReader=true,SingleWriter=false,AllowSynchronousContinuations=false
    });
    private int _remaining;
    private string? _failure;
    internal int ResponseChunk=>Math.Clamp(mtu-3,20,488)-9;
    internal string Mode=>$"{(notifications?$"notify{_notifyCredits}{(unacknowledged?"-command":"")}":"read")}/{(unacknowledged?$"write{_credits}":"ack")}";
    internal void Notify(byte[] packet) {
        if(packet.Length<=8||packet.Length>Math.Clamp(mtu-3,20,488)) {
            Fail("BLE notification length invalid");return;
        }
        if(!_packets.Writer.TryWrite(packet))Fail("BLE notification window overflow");
    }
    private void Fail(string message){Volatile.Write(ref _failure,message);_packets.Writer.TryComplete(new IOException(message));}
    private void CheckFailure(){if(Volatile.Read(ref _failure) is {} failure)throw new IOException(failure);}
    internal async Task<byte[]> ReadAsync(CancellationToken token) {
        CheckFailure();
        if(_remaining==0) {
            if(_packets.Reader.TryPeek(out _)||_packets.Reader.Completion.IsCompleted)
                throw new IOException("BLE unexpected notification outside granted window");
            return await read(token);
        }
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(token);timeout.CancelAfter(TimeSpan.FromSeconds(4));
        byte[] packet;
        try {packet=await _packets.Reader.ReadAsync(timeout.Token);}
        catch(ChannelClosedException ex){throw new IOException("BLE notification queue closed",ex);}
        CheckFailure();
        _remaining--;
        int end=BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(4))+packet.Length-8;
        if(end==BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(6)))_remaining=0;
        return packet;
    }
    internal async Task WriteAsync(byte[] packet,CancellationToken token) {
        if(notifications&&packet.Length==7&&packet[0]==1) {
            if(_remaining>0)return; // peers already queued the rest of this bounded window
            var grant=new byte[8];packet.CopyTo(grant,0);grant[0]=3;grant[7]=(byte)_notifyCredits;
            _remaining=_notifyCredits;
            // The granted packets confirm the exact id/cursor through the
            // receiver's existing checks. Avoid a second ATT round trip where
            // write commands are supported; a lost grant still times out.
            await write(grant,!unacknowledged,token);return;
        }
        bool acknowledged=true;
        if(unacknowledged&&packet.Length==7&&packet[0]==1)acknowledged=false;
        else if(unacknowledged&&packet.Length>9&&packet[0]==2) {
            int offset=BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(5));
            int total=BinaryPrimitives.ReadUInt16LittleEndian(packet.AsSpan(7));
            // The negotiated bounded window and final fragment form ATT barriers.
            // The device rejects gaps, so the final success cannot hide a loss.
            acknowledged=offset+packet.Length-9==total||(offset/ResponseChunk+1)%_credits==0;
            if(writeBatch is not null) {
                _pendingWrites.Add(packet);
                if(!acknowledged)return;
                try {await writeBatch(_pendingWrites,token);} finally {_pendingWrites.Clear();}
                return;
            }
        }
        await write(packet,acknowledged,token);
    }
    internal static async Task WriteBatchOrderedAsync(IReadOnlyList<byte[]> packets,Func<byte[],bool,CancellationToken,Task> send,CancellationToken token) {
        if(packets.Count is <1 or >32)throw new IOException("BLE write window invalid");
        // Await each command to preserve submission order; the final
        // acknowledged write is a barrier before yielding the shared gate.
        for(int i=0;i<packets.Count;i++) {
            token.ThrowIfCancellationRequested();
            await send(packets[i],i==packets.Count-1,token);
        }
    }
}
