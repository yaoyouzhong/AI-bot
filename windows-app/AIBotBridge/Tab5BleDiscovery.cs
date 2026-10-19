using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace AIBotBridge;

// A hint from this process's last authenticated data ACK, never an identity
// substitute. A failed quick retry drops it and resumes the full scan.
internal sealed class Tab5BleDiscovery
{
    private string? _deviceId;
    private ulong _address;
    private int _addressType;
    private bool _quickRetry;
    internal void Remember(string deviceId,ulong address,int addressType) {
        _deviceId=deviceId;_address=address;_addressType=addressType;_quickRetry=true;
    }
    internal bool Matches(string deviceId,ulong address,int addressType)=>
        _deviceId==deviceId&&_address==address&&_addressType==addressType;
    internal int RetryDelay(string? deviceId) {
        if(deviceId==_deviceId&&_quickRetry){_quickRetry=false;return 250;}
        _deviceId=null;_address=0;_quickRetry=false;return 3000;
    }
    // Windows HID can own the existing LE connection, at which point the
    // peripheral no longer advertises. OS-paired addresses are discovery hints
    // only: the caller still verifies the private service's device ID and HMAC.
    internal static bool PairedNameMatches(string deviceId,string name)=>
        deviceId.Length==12&&name=="AI-bot TAB5 "+deviceId[^6..];
    internal static async Task<IReadOnlyList<(ulong Address,BluetoothAddressType Type)>> PairedCandidatesAsync(string deviceId,CancellationToken token) {
        var results=new List<(ulong,BluetoothAddressType)>();
        var devices=await DeviceInformation.FindAllAsync(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true)).AsTask(token);
        foreach(var info in devices.Where(d=>PairedNameMatches(deviceId,d.Name)).Take(4)) {
            using var device=await BluetoothLEDevice.FromIdAsync(info.Id).AsTask(token);
            if(device is not null)results.Add((device.BluetoothAddress,device.BluetoothAddressType));
        }
        return results;
    }
    internal static async Task WaitAsync(Task preferred,CancellationToken token,int maximumMs=4000) {
        using var stop=CancellationTokenSource.CreateLinkedTokenSource(token);
        try {await await Task.WhenAny(preferred,Task.Delay(maximumMs,stop.Token));token.ThrowIfCancellationRequested();}
        finally {stop.Cancel();}
    }
}
