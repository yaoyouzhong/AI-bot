using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;

namespace AIBotBridge;

internal sealed class Tab5BleGattMailbox : IDisposable
{
    private readonly GattCharacteristic _endpoint;
    private readonly bool _subscribed;
    private readonly SemaphoreSlim _gate;
    private long _queuedMs;
    private long _notifyStarted,_notifyLast,_notifyCount,_notifyMaxGap;
    private long _readMs,_commandCount,_commandMs,_ackCount,_ackMs,_batchCount,_batchMs,_queuedBatches;
    private long _nativeLimit,_writeWindow;
    internal long QueuedMilliseconds=>Interlocked.Read(ref _queuedMs);
    internal Tab5BleGattTiming WriteTiming=>new(Interlocked.Read(ref _readMs),Interlocked.Read(ref _commandCount),Interlocked.Read(ref _commandMs),Interlocked.Read(ref _ackCount),Interlocked.Read(ref _ackMs),Interlocked.Read(ref _batchCount),Interlocked.Read(ref _batchMs),Interlocked.Read(ref _queuedBatches),Interlocked.Read(ref _nativeLimit),Interlocked.Read(ref _writeWindow));
    internal string ReceiveDiagnostic=>$"notifyCount={Interlocked.Read(ref _notifyCount)}; notifySpan={Math.Max(0,Interlocked.Read(ref _notifyLast)-Interlocked.Read(ref _notifyStarted))}ms; notifyMaxGap={Interlocked.Read(ref _notifyMaxGap)}ms; readMs={Interlocked.Read(ref _readMs)}; commandCount={Interlocked.Read(ref _commandCount)}; commandWaitSumMs={Interlocked.Read(ref _commandMs)}; ackCount={Interlocked.Read(ref _ackCount)}; ackWaitSumMs={Interlocked.Read(ref _ackMs)}; batches={Interlocked.Read(ref _batchCount)}; batchWallMs={Interlocked.Read(ref _batchMs)}; queuedBatches={Interlocked.Read(ref _queuedBatches)}";
    internal Tab5BleMailboxWindow Window {get;}
    private Tab5BleGattMailbox(GattCharacteristic endpoint,int mtu,bool notifications,int credits,SemaphoreSlim gate,int notifyCredits=0,bool queueBulkResponses=false,int bulkWriteCredits=0,Func<int>? nativeLimit=null,Func<int>? bulkWriteLimit=null) {
        _endpoint=endpoint;_subscribed=notifications;_gate=gate;
        Window=new Tab5BleMailboxWindow(ct=>SerializedAsync(async ()=> {
            ResetWriteTiming();long started=Environment.TickCount64;
            var result=await endpoint.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(ct);
            Interlocked.Exchange(ref _readMs,Environment.TickCount64-started);
            if(result.Status!=GattCommunicationStatus.Success||result.Value.Length>488)throw new IOException("BLE mailbox read failed");
            using var reader=DataReader.FromBuffer(result.Value);var bytes=new byte[result.Value.Length];reader.ReadBytes(bytes);
            Interlocked.Exchange(ref _notifyStarted,0);Interlocked.Exchange(ref _notifyLast,0);
            Interlocked.Exchange(ref _notifyCount,0);Interlocked.Exchange(ref _notifyMaxGap,0);
            return bytes;
        },ct),(bytes,ack,ct)=>SerializedAsync(async ()=>{await WriteAsync(bytes,ack,ct);return true;},ct),
        notifications,endpoint.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse),mtu,credits,
        (packets,ct)=>SerializedAsync(async ()=> {
            long started=Environment.TickCount64;
            bool queued=queueBulkResponses&&packets.Count<=64&&packets[0].Length>9&&
                System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(packets[0].AsSpan(7))>12288;
            Interlocked.Increment(ref _batchCount);if(queued)Interlocked.Increment(ref _queuedBatches);
            try {
                if(queued) {
                    int limit=nativeLimit?.Invoke()??7;
                    Interlocked.Exchange(ref _nativeLimit,Math.Max(Interlocked.Read(ref _nativeLimit),limit));
                    Interlocked.Exchange(ref _writeWindow,Window!.ActiveWriteCredits);
                    await Tab5BleMailboxWindow.WriteBatchQueuedAsync(packets,WriteAsync,ct,limit);
                }
                else await Tab5BleMailboxWindow.WriteBatchOrderedAsync(packets,WriteAsync,ct);
                return true;
            }finally{Interlocked.Add(ref _batchMs,Environment.TickCount64-started);}
        },ct),notifyCredits,bulkWriteCredits,bulkWriteLimit);
    }
    private void ResetWriteTiming() {
        Interlocked.Exchange(ref _commandCount,0);Interlocked.Exchange(ref _commandMs,0);
        Interlocked.Exchange(ref _ackCount,0);Interlocked.Exchange(ref _ackMs,0);
        Interlocked.Exchange(ref _batchCount,0);Interlocked.Exchange(ref _batchMs,0);Interlocked.Exchange(ref _queuedBatches,0);
        Interlocked.Exchange(ref _nativeLimit,0);
        Interlocked.Exchange(ref _writeWindow,0);
    }
    private async Task<T> SerializedAsync<T>(Func<Task<T>> action,CancellationToken token) {
        long queued=Environment.TickCount64;await _gate.WaitAsync(token);
        try {Interlocked.Add(ref _queuedMs,Environment.TickCount64-queued);return await action();}
        finally {_gate.Release();}
    }
    private async Task WriteAsync(byte[] bytes,bool acknowledged,CancellationToken token) {
        using var writer=new DataWriter();writer.WriteBytes(bytes);
        long started=Environment.TickCount64;
        try {
            // No await before native submission: the bounded batch relies on
            // invocation order, while the device still rejects cursor gaps.
            if(await _endpoint.WriteValueAsync(writer.DetachBuffer(),acknowledged?GattWriteOption.WriteWithResponse:GattWriteOption.WriteWithoutResponse).AsTask(token)!=GattCommunicationStatus.Success)
                throw new IOException("BLE mailbox write failed");
        }finally {
            if(acknowledged){Interlocked.Increment(ref _ackCount);Interlocked.Add(ref _ackMs,Environment.TickCount64-started);}
            else{Interlocked.Increment(ref _commandCount);Interlocked.Add(ref _commandMs,Environment.TickCount64-started);}
        }
    }
    internal static async Task<Tab5BleGattMailbox> CreateAsync(GattCharacteristic endpoint,int mtu,CancellationToken token,SemaphoreSlim gate,int credits=4,int notifyCredits=0,bool queueBulkResponses=false,int bulkWriteCredits=0,Func<int>? nativeLimit=null,Func<int>? bulkWriteLimit=null) {
        if(!endpoint.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))return new(endpoint,mtu,false,credits,gate,notifyCredits,queueBulkResponses,bulkWriteCredits,nativeLimit,bulkWriteLimit);
        var adapter=new Tab5BleGattMailbox(endpoint,mtu,true,credits,gate,notifyCredits,queueBulkResponses,bulkWriteCredits,nativeLimit,bulkWriteLimit);
        endpoint.ValueChanged+=adapter.Changed;
        try {
            if(await endpoint.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify).AsTask(token)==GattCommunicationStatus.Success)return adapter;
        }catch {adapter.Dispose();throw;}
        adapter.Dispose();return new(endpoint,mtu,false,credits,gate,notifyCredits,queueBulkResponses,bulkWriteCredits,nativeLimit,bulkWriteLimit); // unsupported CCCD retains legacy reads
    }
    private void Changed(GattCharacteristic sender,GattValueChangedEventArgs args) {
        long now=Environment.TickCount64,previous=Interlocked.Exchange(ref _notifyLast,now);
        Interlocked.CompareExchange(ref _notifyStarted,now,0);Interlocked.Increment(ref _notifyCount);
        if(previous>0) {
            long gap=Math.Max(0,now-previous),seen;
            do {seen=Interlocked.Read(ref _notifyMaxGap);if(gap<=seen)break;}
            while(Interlocked.CompareExchange(ref _notifyMaxGap,gap,seen)!=seen);
        }
        using var reader=DataReader.FromBuffer(args.CharacteristicValue);
        if(reader.UnconsumedBufferLength>488){Window.Notify([]);return;}
        var bytes=new byte[reader.UnconsumedBufferLength];reader.ReadBytes(bytes);Window.Notify(bytes);
    }
    public void Dispose(){if(_subscribed)_endpoint.ValueChanged-=Changed;}
}
