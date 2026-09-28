using System.Security.Cryptography;
namespace AIBotBridge;
internal static class Tab5BleTransferSelfTest
{
    internal static async Task RunAsync()
    {
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
        foreach(var test in new[]{(32768,244,true,0),(1024,20,true,0),(20852,244,false,125)}) {
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
