using System.Buffers.Binary;

namespace AIBotBridge;

internal static class Tab5BleMailboxWindowSelfTest
{
    internal static async Task RunAsync() {
        await VerifyQueuedBatchesAsync();
        await VerifyExtendedQueuedBatchesAsync();
        await VerifyBulkWriteNegotiationAsync();
        await VerifyWriteLimitSnapshotAsync();
        foreach(int credits in new[]{4,8,16,32})foreach(int mtu in new[]{23,247,517})foreach(bool notify in new[]{false,true})foreach(bool fast in new[]{false,true})
            await ExchangeAsync(mtu,notify,fast,0,credits);
        foreach(int credits in new[]{8,32})foreach(int mtu in new[]{23,247,517})await ExchangeAsync(mtu,true,true,0,credits,65535);
        foreach(int mtu in new[]{23,247,517})await ExchangeAsync(mtu,true,true,0,8,65535,32);
        foreach(int credits in new[]{8,16,32})foreach(int mtu in new[]{23,247,517})await ExchangeAsync(mtu,true,true,0,credits,65535,32,true);
        foreach(int credits in new[]{8,16,32})try {await ExchangeAsync(517,true,true,5,credits,65535,32,true);throw new Exception("Queued missing fragment accepted");}catch(IOException){}
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
    private static async Task ExchangeAsync(int mtu,bool notify,bool fast,int fault,int credits=4,int size=12288,int notifyCredits=0,bool queued=false) {
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
        window=new Tab5BleMailboxWindow(_=>{reads++;return Task.FromResult(Part(480));},Send,notify,fast,mtu,credits,
            (packets,ct)=>queued?Tab5BleMailboxWindow.WriteBatchQueuedAsync(packets,Send,ct):Tab5BleMailboxWindow.WriteBatchOrderedAsync(packets,Send,ct),notifyCredits);
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
    private static async Task VerifyQueuedBatchesAsync() {
        foreach(int limit in new[]{7,31})foreach(int fault in new[]{0,1,2,3,4,5}) {
            var packets=Enumerable.Range(0,limit+1).Select(i=>new byte[]{(byte)i}).ToArray();
            var releases=Enumerable.Range(0,limit).Select(_=>new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
            var submissions=new List<int>();var operations=new List<Task>();
            int active=0,peak=0,barriers=0;
            using var cancel=new CancellationTokenSource();
            Task Send(byte[] data,bool ack,CancellationToken ct) {
                int index=data[0];submissions.Add(index);
                if(ack) {
                    if(active!=0)throw new Exception("ATT barrier ran before commands completed");
                    barriers++;return fault==4?Task.FromException(new IOException("barrier failed")):Task.CompletedTask;
                }
                if(fault==3&&index==3)throw new IOException("native submission failed");
                if(fault==5&&index==3)return Task.FromException(new IOException("native operation already failed"));
                active++;peak=Math.Max(peak,active);
                async Task Complete() {
                    try {
                        await releases[index].Task;
                        ct.ThrowIfCancellationRequested();
                        if(fault==1&&index==2)throw new IOException("command failed");
                    }finally{Interlocked.Decrement(ref active);}
                }
                var operation=Complete();operations.Add(operation);return operation;
            }
            var run=Tab5BleMailboxWindow.WriteBatchQueuedAsync(packets,Send,cancel.Token,limit);
            int commands=fault is 3 or 5?3:limit;
            if(peak!=commands||!submissions.SequenceEqual(Enumerable.Range(0,fault is 3 or 5?4:limit))||barriers!=0||run.IsCompleted)
                throw new Exception("Queued commands were serialized, reordered, unbounded or not drained");
            if(fault==2)cancel.Cancel();
            // Complete native operations out of order: completion order must
            // never change submission order or allow an early final ACK.
            for(int i=commands-2;i>=0;i--)releases[i].SetResult();
            try{await Task.WhenAll(operations.Take(commands-1));}catch(Exception ex) when(ex is IOException or OperationCanceledException){}
            if(run.IsCompleted||barriers!=0)throw new Exception("Gate released while a submitted write was still pending");
            releases[commands-1].SetResult();
            try{await run.WaitAsync(TimeSpan.FromSeconds(2));if(fault!=0)throw new Exception("Queued failure accepted");}
            catch(IOException) when(fault is 1 or 3 or 4 or 5){}
            catch(OperationCanceledException) when(fault==2){}
            if(active!=0||barriers!=(fault is 0 or 4?1:0))throw new Exception("Queued batch leaked operations or acknowledged failure");
        }
        int lone=0;await Tab5BleMailboxWindow.WriteBatchQueuedAsync([new byte[1]],(_,ack,_)=>{if(!ack)throw new Exception("Single final packet not acknowledged");lone++;return Task.CompletedTask;},CancellationToken.None);
        if(lone!=1)throw new Exception("Single final packet lost");
        foreach(int size in new[]{0,65})try {
            await Tab5BleMailboxWindow.WriteBatchQueuedAsync(Enumerable.Range(0,size).Select(_=>new byte[1]).ToArray(),(_,_,_)=>throw new Exception("Invalid window submitted"),CancellationToken.None);
            throw new Exception("Invalid queued bound accepted");
        }catch(IOException){}
        foreach(int limit in new[]{0,32})try {
            await Tab5BleMailboxWindow.WriteBatchQueuedAsync([new byte[1]],(_,_,_)=>throw new Exception("Invalid native limit submitted"),CancellationToken.None,limit);
            throw new Exception("Invalid native limit accepted");
        }catch(IOException){}
        Console.WriteLine("BLE_QUEUED_BATCH_OK native7_31_ordered_submission_final_barrier_full_drain_command_failure_cancel_sync_immediate_failure_bounds");
    }
    private static async Task VerifyExtendedQueuedBatchesAsync() {
        foreach(int window in new[]{32,64})foreach(int fault in new[]{0,1,2,3}) {
            var packets=Enumerable.Range(0,window).Select(i=>new byte[]{(byte)i}).ToArray();
            var releases=Enumerable.Range(0,window-1).Select(_=>new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
            var submitted=Enumerable.Range(0,window-1).Select(_=>new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously)).ToArray();
            var operations=new List<Task>();var order=new List<int>();int active=0,peak=0,barriers=0;
            using var cancel=new CancellationTokenSource();
            Task Send(byte[] data,bool ack,CancellationToken ct) {
                int index=data[0];order.Add(index);
                if(ack){if(active!=0||index!=window-1)throw new Exception("Early extended barrier");barriers++;return Task.CompletedTask;}
                if(fault==2&&index==10){submitted[index].SetResult();throw new IOException("Second cohort submission failed");}
                active++;peak=Math.Max(peak,active);
                async Task Complete() {
                    try{await releases[index].Task;ct.ThrowIfCancellationRequested();if(fault==1&&index==9)throw new IOException("Second cohort completion failed");}
                    finally{Interlocked.Decrement(ref active);}
                }
                var operation=Complete();operations.Add(operation);submitted[index].SetResult();return operation;
            }
            var run=Tab5BleMailboxWindow.WriteBatchQueuedAsync(packets,Send,cancel.Token);
            for(int start=0;start<window-1;start+=7) {
                bool failedCohort=fault!=0&&start==7;
                int end=failedCohort&&fault==2?10:Math.Min(start+7,window-1);
                await submitted[failedCohort&&fault==2?10:end-1].Task.WaitAsync(TimeSpan.FromSeconds(2));
                if(peak>7||barriers!=0||!order.SequenceEqual(Enumerable.Range(0,failedCohort&&fault==2?11:end)))throw new Exception("Extended queue bound/order");
                if(failedCohort&&fault==3)cancel.Cancel();
                for(int i=end-2;i>=start;i--)releases[i].SetResult();
                try{await Task.WhenAll(operations.Skip(start).Take(end-start-1));}catch(Exception ex) when(ex is IOException or OperationCanceledException){}
                if(run.IsCompleted||barriers!=0)throw new Exception("Extended window did not drain cohort");
                releases[end-1].SetResult();
                if(failedCohort)break;
            }
            try{await run.WaitAsync(TimeSpan.FromSeconds(2));if(fault!=0)throw new Exception("Extended fault ignored");}
            catch(IOException) when(fault is 1 or 2){}
            catch(OperationCanceledException) when(fault==3){}
            if(active!=0||peak>7||barriers!=(fault==0?1:0))throw new Exception("Extended window leaked native work");
        }
        Console.WriteLine("BLE_EXTENDED_QUEUE_OK window32_64_native7_later_cohort_failure_cancel_full_drain");
    }
    private static async Task VerifyWriteLimitSnapshotAsync() {
        int requested=32,barriers=0;const int total=49317;
        var window=new Tab5BleMailboxWindow(_=>throw new Exception("Unexpected read"),(_,ack,_)=>{if(ack)barriers++;return Task.CompletedTask;},false,true,517,8,
            bulkWriteCredits:64,bulkWriteLimit:()=>requested);
        foreach(int expected in new[]{4,2}) {
            barriers=0;
            for(int at=0;at<total;at+=window.ResponseChunk) {
                var p=new byte[9+Math.Min(window.ResponseChunk,total-at)];p[0]=2;
                BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(5),(ushort)at);BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(7),total);
                await window.WriteAsync(p,CancellationToken.None);
                requested=64; // A changed selector must wait for the next response.
            }
            if(barriers!=expected)throw new Exception("Write window changed within response or failed to refresh");
        }
        Console.WriteLine("BLE_WINDOW_SNAPSHOT_OK limit_frozen_per_response_next_response_refresh");
    }
    private static async Task VerifyBulkWriteNegotiationAsync() {
        foreach(var sample in new[]{("{}",8),("{\"rpcMailboxWindow\":32,\"rpcNotifyWindow\":32}",8),("{\"rpcWriteWindow\":16}",16),("{\"rpcWriteWindow\":32}",32),
            ("{\"rpcWriteWindow\":64}",64),("{\"rpcWriteWindow\":0}",8),("{\"rpcWriteWindow\":65}",8),("{\"rpcWriteWindow\":\"32\"}",8),("{\"rpcWriteWindow\":null}",8)}) {
            using var info=System.Text.Json.JsonDocument.Parse(sample.Item1);
            if(Tab5BleClient.BulkWriteCredits(info.RootElement,8)!=sample.Item2)throw new Exception("Bulk write capability fallback");
        }
        foreach(int peer in new[]{8,32,64})foreach(int limit in new[]{0,32,64})foreach(int mtu in new[]{23,247,517})foreach(int total in new[]{5274,12288,12289,49317,65535})foreach(bool fast in new[]{false,true}) {
            int fragments=0,barriers=0,received=0;
            Task Send(byte[] p,bool ack,CancellationToken ct) {
                if(BinaryPrimitives.ReadUInt16LittleEndian(p.AsSpan(5))!=received)throw new Exception("Bulk write order");
                received+=p.Length-9;fragments++;if(ack)barriers++;return Task.CompletedTask;
            }
            var window=new Tab5BleMailboxWindow(_=>throw new Exception("Unexpected read"),Send,false,fast,mtu,8,
                (packets,ct)=>Tab5BleMailboxWindow.WriteBatchQueuedAsync(packets,Send,ct),bulkWriteCredits:peer,bulkWriteLimit:limit==0?null:()=>limit);
            for(int at=0;at<total;at+=window.ResponseChunk) {
                var p=new byte[9+Math.Min(window.ResponseChunk,total-at)];p[0]=2;
                BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(5),(ushort)at);BinaryPrimitives.WriteUInt16LittleEndian(p.AsSpan(7),(ushort)total);
                await window.WriteAsync(p,CancellationToken.None);
            }
            int credits=total>12288?Math.Min(peer,limit==0?64:limit):8;
            if(received!=total||barriers!=(fast?(fragments+credits-1)/credits:fragments))throw new Exception("Bulk/legacy ACK boundary");
            if(mtu==517&&total==49317&&fast&&credits==64&&barriers!=2)throw new Exception("48 KiB reply did not use two barriers");
        }
        Console.WriteLine("BLE_WRITE_NEGOTIATION_OK peer8_32_64_default64_override32_small8_legacy_ACK_MTU23_247_517_two_barriers");
    }
}
