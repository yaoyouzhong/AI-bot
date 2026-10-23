using System.Text.Json;

namespace AIBotBridge;

internal sealed partial class Tab5Service
{
    private readonly object _bootVerifyLock=new();
    private sealed record BootProbe(string DeviceId,Tab5OtaPackage Image,string Id,string Transport,long Expires,TaskCompletionSource<JsonElement> Completion);
    private BootProbe? _bootProbe;
    internal static bool SupportsWirelessBoot(string firmware)=>firmware.EndsWith("-ui",StringComparison.Ordinal)&&
        Version.TryParse(firmware[..^3],out var version)&&version>=new Version(0,2,226);
    private object? BootVerifyCommand {get{lock(_bootVerifyLock)return _bootProbe is {} p?
        new{version=1,requestId=p.Id,transport=p.Transport,expiresAt=p.Expires}:null;}}
    private string WirelessBootTransport=>WifiBridgeReady(Environment.TickCount64,Interlocked.Read(ref _wifiRequestAt))?"Wi-Fi":
        DeviceView.Bluetooth=="已连接"?"BLE":"";

    // Called only after the existing pairing, HMAC, AES-GCM, session/time and
    // replay checks. No public unauthenticated diagnostic endpoint is added.
    private (int Status,object Body) ReceiveBootProbe(JsonElement root,string deviceId,string transport) {
        lock(_bootVerifyLock) {
            var p=_bootProbe;
            if(p is null||p.DeviceId!=deviceId||p.Transport!=transport||Text(root,"requestId")!=p.Id||
                !ReferenceEquals(Volatile.Read(ref _ota),p.Image)||DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()>p.Expires)
                return(409,new{error="boot_probe_expired"});
            if(!root.TryGetProperty("hello",out var hello)||hello.ValueKind!=JsonValueKind.Object||
                !root.TryGetProperty("diagnostic",out var diagnostic)||diagnostic.ValueKind!=JsonValueKind.Object)
                return(400,new{error="invalid_boot_report"});
            return p.Completion.TrySetResult(root.Clone())?(200,new{accepted=true}):(409,new{error="boot_probe_consumed"});
        }
    }
    internal static long ValidateWirelessBootReport(JsonElement root,Tab5OtaPackage image,string deviceId,long previous) {
        long uptime=Tab5InstallBootCheck.Hello(root.GetProperty("hello"),deviceId,image.Version,previous);
        VerifyUpgradeDiagnostic(root.GetProperty("diagnostic"),image);
        return uptime;
    }
    private async Task VerifyWirelessBootAsync(Tab5OtaPackage image,string deviceId,CancellationToken token) {
        string transport=WirelessBootTransport;
        if(!SupportsWirelessBoot(DeviceView.Firmware))throw new IOException("当前固件不支持无线启动核验，请升级至 0.2.226-ui 或连接 USB。");
        if(transport.Length==0)throw new IOException("没有可用的已配对无线连接。");
        lock(_lifecycle){if(_stopping||Busy)throw new InvalidOperationException("设备仍在处理操作，请稍后核验。");_voiceRequests++;}
        _verifiedBoot=null;_upgradeDiagnostic="正在通过 "+transport+" 核验";
        try {
            long previous=-1;JsonElement last=default;
            for(int i=0;i<3;i++) {
                token.ThrowIfCancellationRequested();
                var probe=new BootProbe(deviceId,image,Guid.NewGuid().ToString("N"),transport,DateTimeOffset.UtcNow.AddSeconds(10).ToUnixTimeMilliseconds(),new(TaskCreationOptions.RunContinuationsAsynchronously));
                lock(_bootVerifyLock)_bootProbe=probe;
                Republish();
                try{last=await probe.Completion.Task.WaitAsync(TimeSpan.FromSeconds(10),token);}
                finally{lock(_bootVerifyLock){if(ReferenceEquals(_bootProbe,probe))_bootProbe=null;}}
                if(PairedId!=deviceId||!ReferenceEquals(Volatile.Read(ref _ota),image))throw new IOException("设备或所选固件已变化，请重新核验。");
                previous=ValidateWirelessBootReport(last,image,deviceId,previous);
                if(i<2)await Task.Delay(1200,token);
            }
            token.ThrowIfCancellationRequested();
            if(PairedId!=deviceId||!ReferenceEquals(Volatile.Read(ref _ota),image))throw new IOException("设备或所选固件已变化，请重新核验。");
            RecordVerifiedBoot(last.GetProperty("diagnostic"),image,deviceId);
            _upgradeDiagnostic=$"verified; transport={transport}; firmware={image.Version}; elfSha256={image.ElfSha256}; partition={last.GetProperty("diagnostic").GetProperty("partition").GetString()}; state=VALID; liveSamples=3; timing=unavailable";
        }catch(TimeoutException){_upgradeDiagnostic="failed; wireless_timeout";throw new IOException("无线启动核验超时，未确认成功；请检查设备连接后重试。");}
        catch(Exception ex){_upgradeDiagnostic="failed; "+ex.GetType().Name;throw;}
        finally{lock(_bootVerifyLock)_bootProbe=null;lock(_lifecycle)_voiceRequests--;Republish();}
    }
}
