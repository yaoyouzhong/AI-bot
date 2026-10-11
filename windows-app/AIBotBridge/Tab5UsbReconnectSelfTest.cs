using System.Buffers.Binary;

namespace AIBotBridge;

internal static class Tab5UsbReconnectSelfTest
{
    private sealed class BrokenClose(Exception failure):IDisposable {public void Dispose()=>throw failure;}
    internal static async Task RunAsync()
    {
        using var gate=new SemaphoreSlim(1,1);
        long generation=1;int writes=0,closed=0;
        void Close(){closed++;Interlocked.Increment(ref generation);}
        Task<T> Transfer<T>(long owner,Func<Task<T>> body,CancellationToken token=default)=>
            Tab5UsbRecovery.InSessionAsync(gate,owner,()=>Interlocked.Read(ref generation),body,Close,_=>{},token);
        var entered=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Tab5BleVoice Pump(long owner,bool pause)=>new(
            ct=>Transfer(owner,()=>{
                var part=new byte[132];BinaryPrimitives.WriteUInt32LittleEndian(part,42);
                BinaryPrimitives.WriteUInt16LittleEndian(part.AsSpan(6),124);
                return Task.FromResult(part);
            },ct),
            async (_,ct)=>{await Transfer(owner,()=>{writes++;return Task.FromResult(0);},ct);},
            async (_,_,_,ct)=>{if(pause){entered.SetResult();await release.Task.WaitAsync(ct);}return (200,new byte[28]);},247);
        // Real mailbox pump waits outside the gate while the old port closes
        // and an identically configured replacement becomes ready.
        var old=Pump(generation,true);var running=old.PumpAsync(CancellationToken.None);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await gate.WaitAsync();try{Close();}finally{gate.Release();}
        release.SetResult();
        try{await running;throw new Exception("Old RPC crossed reconnect");}
        catch(IOException ex) when(ex.Message=="USB RPC connection replaced"){}
        if(writes!=0||closed!=1||old.CompletedCount!=0)throw new Exception("Stale handler wrote to or closed replacement port");
        var fresh=Pump(generation,false);
        if(!await fresh.PumpAsync(CancellationToken.None)||writes!=1||fresh.CompletedCount!=1)throw new Exception("New mailbox request could not complete");

        // Ownership must also be checked after a queued gate wait, not just
        // before it. Same chunk size and reused mailbox ID do not prove identity.
        await gate.WaitAsync();long queuedGeneration=generation;
        var queued=Transfer(queuedGeneration,()=>{writes++;return Task.FromResult(0);});
        Close();gate.Release();
        try{await queued;throw new Exception("Queued old RPC crossed reconnect");}
        catch(IOException ex) when(ex.Message=="USB RPC connection replaced"){}
        if(writes!=1||closed!=2)throw new Exception("Queued stale RPC touched new connection");

        var nativeFailure=new System.ComponentModel.Win32Exception(121);
        if(!Tab5UsbRecovery.IsDisconnect(nativeFailure,CancellationToken.None))throw new Exception("Native serial error stops reconnect worker");
        foreach(Exception closeFailure in new Exception[]{new IOException(),new InvalidOperationException(),nativeFailure})
            if(Tab5Service.DisposeUsbPort(new BrokenClose(closeFailure)))throw new Exception("Native cleanup failure reported success");
        foreach(Exception fault in new Exception[]{new IOException(),new TimeoutException(),new FormatException(),new OperationCanceledException(),nativeFailure}) {
            int before=closed;
            try{await Transfer<int>(generation,()=>throw fault);throw new Exception("Partial transaction failure swallowed");}
            catch(Exception ex) when(ReferenceEquals(ex,fault)){}
            if(closed!=before+1||gate.CurrentCount!=1)throw new Exception("Poisoned transaction retained port or gate");
        }
        await gate.WaitAsync();using var stop=new CancellationTokenSource();int beforeCancel=closed;
        var cancelled=Transfer<int>(generation,()=>throw new Exception("Cancelled waiter ran"),stop.Token);
        stop.Cancel();
        try{await cancelled;throw new Exception("Queued cancellation ignored");}catch(OperationCanceledException){}
        gate.Release();
        if(closed!=beforeCancel)throw new Exception("Cancelled waiter closed another owner's port");
        Console.WriteLine("USB_RECONNECT_SESSION_OK delayed handler and queued RPC cannot write/close replacement; fresh same-ID request succeeds; text/binary failures close once; cancelled waiter preserves owner");
    }
}
