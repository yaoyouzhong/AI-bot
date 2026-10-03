using System.Security.Cryptography;
namespace AIBotBridge;
internal static class Tab5BleTransferSelfTest
{
    internal static async Task RunAsync()
    {
        // A photo spanning several RPC chunks should keep telemetry out of
        // inter-chunk gaps, while a long/stuck upload must retain fresh status.
        long lastTelemetry=0,maxGap=0;int updatesDuringPhoto=0,updatesDuringLongUpload=0;
        for(long now=250;now<=24000;now+=250) {
            bool receiving=now>=1000&&now<1700||now>=1800&&now<4000;
            bool uploadActive=now>=1700&&now<20000;
            if(Tab5BleClient.DeferTelemetry(now,lastTelemetry,receiving,uploadActive))continue;
            maxGap=Math.Max(maxGap,now-lastTelemetry);lastTelemetry=now;
            if(now>=1000&&now<4000)updatesDuringPhoto++;
            if(now>=4000&&now<20000)updatesDuringLongUpload++;
        }
        if(updatesDuringPhoto!=0||updatesDuringLongUpload<2||maxGap>=8000)
            throw new Exception("Photo priority lost or long upload starved the eight-second freshness budget");
        if(Tab5BleClient.VoicePollDelay(5000,4950,true)>15||Tab5BleClient.VoicePollDelay(5000,0,true)>500||Tab5BleClient.VoicePollDelay(5000,0,false)!=250)
            throw new Exception("Photo traffic delayed active voice or altered normal idle polling");
        using(var gate=new SemaphoreSlim(1,1)) {
            var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            bool rpcEntered=false;
            var status=Tab5BleTransfer.SerializeAsync(gate,async ()=>{entered.SetResult();await release.Task;return 1;},CancellationToken.None);
            await entered.Task;
            var rpc=Tab5BleTransfer.SerializeAsync(gate,()=>{rpcEntered=true;return Task.FromResult(2);},CancellationToken.None);
            if(rpcEntered)throw new Exception("RPC overlapped an unfinished status/ACK exchange");
            using var queuedCancel=new CancellationTokenSource();
            var queued=Tab5BleTransfer.SerializeAsync<bool>(gate,()=>throw new Exception("Cancelled exchange ran"),queuedCancel.Token);
            queuedCancel.Cancel();
            try{await queued;throw new Exception("Queued cancellation ignored");}catch(OperationCanceledException){}
            release.SetResult();await status;await rpc;
            if(!rpcEntered||gate.CurrentCount!=1)throw new Exception("Queued RPC did not resume or gate leaked");
            try{await Tab5BleTransfer.SerializeAsync<bool>(gate,()=>throw new IOException("injected"),CancellationToken.None);}catch(IOException){}
            if(gate.CurrentCount!=1)throw new Exception("Failed exchange leaked ATT ownership");
        }
        foreach(var size in new[]{20,244})foreach(var length in new[]{32,1024,21821,32768}) {
            int pending=0,barriers=0;
            for(int offset=0;offset<length;offset+=size) {
                int count=Math.Min(size,length-offset);pending++;
                if(Tab5BleTransfer.RequiresResponse(offset,count,length,size,true)){barriers++;pending=0;}
                if(pending>=16)throw new Exception("BLE queue window exceeded");
                if(!Tab5BleTransfer.RequiresResponse(offset,count,length,size,false))throw new Exception("Legacy write lost response");
            }
            if(pending!=0||barriers==0)throw new Exception("Final BLE drain barrier missing");
        }
        // A photo RPC leaves the controller busy. The following 13.5 KB status
        // must allow drain latency without exceeding the four-command queue.
        {
            var packet=RandomNumberGenerator.GetBytes(13545);int offset=0,pending=0;bool acked=false;
            await Tab5BleTransfer.SendAsync(packet,244,true,async(part,ct)=> {
                pending++;
                if(pending>4)throw new Exception("Photo upload flooded the controller queue");
                await Task.Delay(200,ct);
                if(Tab5BleTransfer.RequiresResponse(offset,part.Length,packet.Length,244,true,4))pending=0;
                offset+=part.Length;
            },ct=>{acked=offset==packet.Length&&pending==0;return Task.FromResult(acked);},CancellationToken.None,4);
            if(!acked)throw new Exception("Photo interleaved status lost its complete ACK");
            Console.WriteLine("TAB5_PHOTO_STATUS_PACING_OK 13.5KB status, four-command queue, >10s radio delivery, final ACK");
        }
        foreach(var test in new[]{(32768,244,true,0),(32768,488,true,0),(1024,20,true,0),(20852,244,false,125)}) {
            var packet=RandomNumberGenerator.GetBytes(test.Item1);
            var received=new List<byte>();int ackReads=0;
            await Tab5BleTransfer.SendAsync(packet,test.Item2,test.Item3,async (part,ct)=> {
                received.AddRange(part.ToArray());
                if(test.Item4>0)await Task.Delay(test.Item4,ct);
            },ct=>Task.FromResult(++ackReads>=2&&received.SequenceEqual(packet)),CancellationToken.None);
            if(!received.SequenceEqual(packet)||ackReads!=2)throw new Exception("BLE data/ACK mismatch");
        }
        using(var cancelled=new CancellationTokenSource()) {
            int writes=0,acks=0;
            try {
                await Tab5BleTransfer.SendAsync(new byte[1024],244,true,(part,ct)=> {
                    if(++writes==2)cancelled.Cancel();return Task.CompletedTask;
                },ct=>{acks++;return Task.FromResult(true);},cancelled.Token);
                throw new Exception("Cancelled frame was accepted");
            }catch(OperationCanceledException){if(writes!=2||acks!=0)throw new Exception("Cancellation continued transfer");}
        }
        bool rejected=false;
        try {await Tab5BleTransfer.SendAsync(new byte[32],244,true,(_,_)=>Task.CompletedTask,_=>Task.FromResult(false),CancellationToken.None);}
        catch(OperationCanceledException){rejected=true;}
        if(!rejected)throw new Exception("Missing frame acknowledgement accepted");
        Console.WriteLine("TAB5_BLE_TRANSFER_PASS: large frames, small MTU, >10s legacy transfer, cancellation, required ACK");
    }
}
