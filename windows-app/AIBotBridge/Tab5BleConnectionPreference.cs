using System.Runtime.InteropServices;
using Windows.Devices.Bluetooth;

namespace AIBotBridge;

// Session-scoped request, never a system-wide Bluetooth setting. Keep Win10
// support and do not retry an unsupported/rejected request on every poll.
internal sealed class Tab5BleConnectionPreference(Func<bool,IDisposable?> request) : IDisposable
{
    private IDisposable? _request;
    private bool _bulk;
    internal void Update(bool bulk) {
        if(bulk==_bulk)return;
        _bulk=bulk;
        _request?.Dispose();_request=null;
        _request=request(bulk);
    }
    public void Dispose(){_request?.Dispose();_request=null;_bulk=false;}
    internal static string ReadLink(BluetoothLEDevice device) {
        if(!OperatingSystem.IsWindowsVersionAtLeast(10,0,22000))return "actualLink=unavailable_Windows10";
        try {
            var parameters=device.GetConnectionParameters();var phy=device.GetConnectionPhy();
            string Phy(BluetoothLEConnectionPhyInfo value)=>value.IsUncoded2MPhy?"2M":value.IsUncoded1MPhy?"1M":value.IsCodedPhy?"coded":"unknown";
            return $"actualIntervalUs={parameters.ConnectionInterval*1250}; latency={parameters.ConnectionLatency}; supervisionMs={parameters.LinkTimeout*10}; txPhy={Phy(phy.TransmitInfo)}; rxPhy={Phy(phy.ReceiveInfo)}";
        }catch(Exception ex) when(ex is COMException or NotSupportedException or UnauthorizedAccessException or InvalidOperationException) {
            return $"actualLink=unavailable/{ex.GetType().Name}/0x{ex.HResult:X8}";
        }
    }
    internal static Tab5BleConnectionPreference ForDevice(BluetoothLEDevice device,Action<string> diagnostic)=>new(bulk=> {
        if(!OperatingSystem.IsWindowsVersionAtLeast(10,0,22000)) {diagnostic("Windows 10：保留默认连接参数");return null;}
        try {
            var requested=device.RequestPreferredConnectionParameters(bulk?BluetoothLEPreferredConnectionParameters.ThroughputOptimized:BluetoothLEPreferredConnectionParameters.Balanced);
            diagnostic($"{(bulk?"吞吐优先":"均衡")}: {requested.Status}（实际速率以测速为准）");
            return requested;
        }catch(Exception ex) when(ex is COMException or NotSupportedException or UnauthorizedAccessException or InvalidOperationException) {
            diagnostic($"连接参数申请未生效：{ex.GetType().Name} / 0x{ex.HResult:X8}");return null;
        }
    });
}
