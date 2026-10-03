using System.Buffers.Binary;

namespace AIBotBridge;

internal static class Tab5BleMailboxWindowSelfTest
{
    internal static async Task RunAsync() {
        foreach(int credits in new[]{4,8,16,32})foreach(int mtu in new[]{23,247,517})foreach(bool notify in new[]{false,true})foreach(bool fast in new[]{false,true})
            await ExchangeAsync(mtu,notify,fast,0,credits);
        foreach(int credits in new[]{8,32})foreach(int mtu in new[]{23,247,517})await ExchangeAsync(mtu,true,true,0,credits,65535);
        foreach(int mtu in new[]{23,247,517})await ExchangeAsync(mtu,true,true,0,8,65535,32);
        foreach(int credits in new[]{4,32})foreach(int fault in new[]{1,2,3,4,5,6}) {
            try {await ExchangeAsync(517,true,true,fault,credits);throw new Exception("Corrupt notification reached handler");}
            catch(IOException){}
            catch(OperationCanceledException) when(fault is 4 or 6){}
        }
        foreach(int credits in new[]{4,8,32}) {
            var overflow=new Tab5BleMailboxWindow(_=>throw new Exception("Unexpected read"),(_,_,_)=>Task.CompletedTask,true,true,517,credits);
            for(int i=0;i<=credits;i++)overflow.Notify(new byte[488]);
            try {await overflow.ReadAsync(CancellationToken.None);throw new Exception("Overflow accepted");}catch(IOException){}
        }
        Console.WriteLine("TAB5_BLE_WINDOW_PASS MTU 23/247/517; notify/legacy; grant command/ack; exact bytes; final ATT barrier; stale/order/drop/grant-loss/cancel/overflow rejected");
    }
    private static async Task ExchangeAsync(int mtu,bool notify,bool fast,int fault,int credits=4,int size=12288,int notifyCredits=0) {
        int receiveCredits=notifyCredits>0?notifyCredits:credits;
        byte[] request=Enumerable.Range(0,size).Select(i=>(byte)(i%251)).ToArray();
        byte[] response=Enumerable.Range(0,size==65535?65535:5274).Select(i=>(byte)(i%239)).ToArray();
        int at=0,reads=0,grants=0,handled=0,responsePackets=0,barriers=0;
        var received=new List<byte>();bool finalBarrier=false;
        byte[] Part(int count) {
            count=Math.Min(count,request.Length-at);var p=new byte[8+count];
            BinaryPrimitives.WriteUInt32LittleEndian(p,42);BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(4),(ushort)at);
            BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(6),(ushort)request.Length);request.AsSpan(at,count).CopyTo(p.AsSpan(8));return p;
        }
        Tab5BleMailboxWindow? window=null;
        Task Send(byte[] p,bool ack,CancellationToken ct) {
            ct.ThrowIfCancellationRequested();
            if(p[0]==1){at=BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(5));if(ack==fast)throw new Exception("Cursor ACK option");}
            else if(p[0]==3) {
                if(ack==fast||!notify||p[7]!=receiveCredits)throw new Exception("Notification grant invalid");
                if(fault==6)return Task.CompletedTask; // lost grant: no packets and no handler
                grants++;at=BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(5));
                for(int i=0;i<receiveCredits&&at<request.Length;i++) {
                    var part=Part(Math.Min(mtu-3,488)-8);at+=part.Length-8;
                    if(fault==4)continue; // whole window missing: bounded cancellation, no handler
                    if(fault==1&&i==0)part[0]++; // stale id
                    if(fault==2&&i==0)part[4]++; // bad offset
                    if(fault==3&&i==0)continue; // one dropped fragment
                    window!.Notify(part);
                }
            } else {
                if(fault==5) {
                    if(responsePackets++==0)return Task.CompletedTask; // controller loses first command
                    if(BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(5))!=received.Count) {
                        if(ack)throw new IOException("Device rejected response gap at ATT barrier");
                        return Task.CompletedTask;
                    }
                }
                if(p.Length>mtu-3||p.Length>488||BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(5))!=received.Count)
                    throw new Exception("Response bounds/order");
                responsePackets++;if(ack)barriers++;
                received.AddRange(p[9..]);finalBarrier=ack;
            }
            return Task.CompletedTask;
        }
        window=new Tab5BleMailboxWindow(_=>{reads++;return Task.FromResult(Part(480));},Send,notify,fast,mtu,credits,(packets,ct)=>Tab5BleMailboxWindow.WriteBatchOrderedAsync(packets,Send,ct),notifyCredits);
        var pump=new Tab5BleVoice(window.ReadAsync,window.WriteAsync,(_,_,body,_)=> {
            handled++;if(fault is not (0 or 5)||!body.SequenceEqual(request[96..]))throw new Exception("Notification reassembly corrupt");
            return Task.FromResult<(int,byte[]?)>((200,response));
        },mtu,maximumResponse:size==65535?65535:12288,maximumRequest:size,responseChunk:window.ResponseChunk,transportMode:window.Mode);
        using var cancel=new CancellationTokenSource();if(fault is 4 or 6)cancel.CancelAfter(30);
        await pump.PumpAsync(cancel.Token);
        if(handled!=1||!received.SequenceEqual(response)||!finalBarrier||barriers!=(fast?(responsePackets+credits-1)/credits:responsePackets))
            throw new Exception("Window response bytes/barriers");
        if(notify&&(reads!=1||grants!=(request.Length-480+receiveCredits*(Math.Min(mtu-3,488)-8)-1)/(receiveCredits*(Math.Min(mtu-3,488)-8))))
            throw new Exception("Window did not replace per-fragment reads");
        if(!notify&&(grants!=0||reads!=(request.Length+479)/480))throw new Exception("Legacy read fallback");
    }
}
