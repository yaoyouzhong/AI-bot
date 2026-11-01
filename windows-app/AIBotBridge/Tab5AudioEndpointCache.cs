using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace AIBotBridge;

// Metadata only: no microphone/capture/IME is opened by prefetch. Device events
// invalidate both directions; opening a take still checks the actual endpoint.
internal sealed class Tab5AudioEndpointCache : IMMNotificationClient, IDisposable
{
    private readonly object _gate=new();
    private readonly object _readGate=new();
    private readonly Dictionary<DataFlow,(long At,int Generation,Tab5AudioDevice[] Devices)> _items=[];
    private readonly Func<DataFlow,Tab5AudioDevice[]> _read;
    private readonly Func<long> _clock;
    private MMDeviceEnumerator? _notifications;
    private readonly bool _cacheEnabled;
    private readonly System.Threading.Timer? _refreshTimer;
    private bool _disposed;
    private int _generation;
    internal Tab5AudioEndpointCache(Func<DataFlow,Tab5AudioDevice[]>? read=null,Func<long>? clock=null,bool automaticRefresh=true) {
        _read=read??Tab5VoiceAudio.Devices;_clock=clock??(()=>Environment.TickCount64);
        _cacheEnabled=read is not null;
        if(read is null)try {_notifications=new();_notifications.RegisterEndpointNotificationCallback(this);}
        catch(System.Runtime.InteropServices.COMException){_notifications?.Dispose();_notifications=null;}
        _cacheEnabled|=_notifications is not null;
        // Refresh metadata before its five-minute safety expiry. Long idle
        // intervals must not put device enumeration back on the next take.
        if(_cacheEnabled&&automaticRefresh)_refreshTimer=new(_=>Refresh(),null,60000,60000);
    }
    internal Tab5AudioDevice[] Get(DataFlow flow) {
        lock(_gate) {
            ObjectDisposedException.ThrowIf(_disposed,this);
            if(_cacheEnabled&&_items.TryGetValue(flow,out var entry)&&entry.Generation==_generation&&_clock()-entry.At<300000)return entry.Devices;
        }
        return Read(flow,false);
    }
    private Tab5AudioDevice[] Read(DataFlow flow,bool refresh) {
        // Single flight includes prefetch, foreground fallback and timer work.
        lock(_readGate)for(int retry=0;retry<3;retry++) {
            int generation;
            lock(_gate) {
                ObjectDisposedException.ThrowIf(_disposed,this);
                generation=_generation;
                if(_cacheEnabled&&_items.TryGetValue(flow,out var entry)&&entry.Generation==generation&&
                    _clock()-entry.At<(refresh?120000:300000))return entry.Devices;
            }
            var devices=_read(flow);
            lock(_gate) {
                ObjectDisposedException.ThrowIf(_disposed,this);
                if(generation!=_generation)continue; // unplug during enumeration
                _items[flow]=(_clock(),generation,devices);
                return devices;
            }
        }
        throw new InvalidOperationException("音频设备正在变化，请稍后重新开始");
    }
    internal void Refresh() {
        DataFlow[] flows;
        lock(_gate){if(_disposed)return;flows=[.._items.Keys];}
        foreach(var flow in flows)try{Read(flow,true);}
        catch(Exception ex) when(ex is System.Runtime.InteropServices.COMException or InvalidOperationException or NAudio.MmException) {
            // Keep the last metadata, but never renew its timestamp on failure.
            // Get still refreshes synchronously after the safety expiry.
            Tab5VoiceTiming.Log("audio-metadata-refresh-failed",Environment.TickCount64);
        }
    }
    internal void Invalidate(){lock(_gate){_generation++;_items.Clear();}}
    public void OnDeviceStateChanged(string id,DeviceState state)=>Invalidate();
    public void OnDeviceAdded(string id)=>Invalidate();
    public void OnDeviceRemoved(string id)=>Invalidate();
    public void OnDefaultDeviceChanged(DataFlow flow,Role role,string id)=>Invalidate();
    public void OnPropertyValueChanged(string id,PropertyKey key)=>Invalidate();
    public void Dispose(){lock(_gate){if(_disposed)return;_disposed=true;_items.Clear();}_refreshTimer?.Dispose();if(_notifications is {} notifications){_notifications=null;notifications.UnregisterEndpointNotificationCallback(this);notifications.Dispose();}}
}
