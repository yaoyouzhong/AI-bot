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
    internal static async Task WaitAsync(Task preferred,CancellationToken token,int maximumMs=4000) {
        using var stop=CancellationTokenSource.CreateLinkedTokenSource(token);
        try {await await Task.WhenAny(preferred,Task.Delay(maximumMs,stop.Token));token.ThrowIfCancellationRequested();}
        finally {stop.Cancel();}
    }
}
