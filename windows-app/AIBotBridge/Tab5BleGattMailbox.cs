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
    internal long QueuedMilliseconds=>Interlocked.Read(ref _queuedMs);
    internal string ReceiveDiagnostic=>$"notifyCount={Interlocked.Read(ref _notifyCount)}; notifySpan={Math.Max(0,Interlocked.Read(ref _notifyLast)-Interlocked.Read(ref _notifyStarted))}ms; notifyMaxGap={Interlocked.Read(ref _notifyMaxGap)}ms";
    internal Tab5BleMailboxWindow Window {get;}
    private Tab5BleGattMailbox(GattCharacteristic endpoint,int mtu,bool notifications,int credits,SemaphoreSlim gate,int notifyCredits=0) {
        _endpoint=endpoint;_subscribed=notifications;_gate=gate;
        Window=new Tab5BleMailboxWindow(ct=>SerializedAsync(async ()=> {
            var result=await endpoint.ReadValueAsync(BluetoothCacheMode.Uncached).AsTask(ct);
            if(result.Status!=GattCommunicationStatus.Success||result.Value.Length>488)throw new IOException("BLE mailbox read failed");
            using var reader=DataReader.FromBuffer(result.Value);var bytes=new byte[result.Value.Length];reader.ReadBytes(bytes);
            Interlocked.Exchange(ref _notifyStarted,0);Interlocked.Exchange(ref _notifyLast,0);
            Interlocked.Exchange(ref _notifyCount,0);Interlocked.Exchange(ref _notifyMaxGap,0);
            return bytes;
        },ct),(bytes,ack,ct)=>SerializedAsync(async ()=>{await WriteAsync(bytes,ack,ct);return true;},ct),
        notifications,endpoint.CharacteristicProperties.HasFlag(GattCharacteristicProperties.WriteWithoutResponse),mtu,credits,
        (packets,ct)=>SerializedAsync(async ()=>{await Tab5BleMailboxWindow.WriteBatchOrderedAsync(packets,WriteAsync,ct);return true;},ct),notifyCredits);
    }
    private async Task<T> SerializedAsync<T>(Func<Task<T>> action,CancellationToken token) {
        long queued=Environment.TickCount64;await _gate.WaitAsync(token);
        try {Interlocked.Add(ref _queuedMs,Environment.TickCount64-queued);return await action();}
        finally {_gate.Release();}
    }
    private async Task WriteAsync(byte[] bytes,bool acknowledged,CancellationToken token) {
        using var writer=new DataWriter();writer.WriteBytes(bytes);
        if(await _endpoint.WriteValueAsync(writer.DetachBuffer(),acknowledged?GattWriteOption.WriteWithResponse:GattWriteOption.WriteWithoutResponse).AsTask(token)!=GattCommunicationStatus.Success)
            throw new IOException("BLE mailbox write failed");
    }
    internal static async Task<Tab5BleGattMailbox> CreateAsync(GattCharacteristic endpoint,int mtu,CancellationToken token,SemaphoreSlim gate,int credits=4,int notifyCredits=0) {
        if(!endpoint.CharacteristicProperties.HasFlag(GattCharacteristicProperties.Notify))return new(endpoint,mtu,false,credits,gate);
        var adapter=new Tab5BleGattMailbox(endpoint,mtu,true,credits,gate,notifyCredits);
        endpoint.ValueChanged+=adapter.Changed;
        try {
            if(await endpoint.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify).AsTask(token)==GattCommunicationStatus.Success)return adapter;
        }catch {adapter.Dispose();throw;}
        adapter.Dispose();return new(endpoint,mtu,false,credits,gate); // unsupported CCCD retains legacy reads
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
