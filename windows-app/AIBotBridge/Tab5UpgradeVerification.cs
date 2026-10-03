using System.Text.Json;

namespace AIBotBridge;
internal sealed partial class Tab5Service
{
    private volatile string _upgradeDiagnostic="尚未核验";
    internal async Task VerifyUpgradeAsync(CancellationToken token) {
        var image=Volatile.Read(ref _ota)??throw new InvalidOperationException("请先选择本次升级的固件，用于核对版本和指纹。");
        var pair=_store.Current??throw new IOException("TAB5 尚未配对。");
        var device=FlashDeviceDiscovery.Read().FirstOrDefault(d=>d.Identity==pair.UsbIdentity)??throw new IOException("启动核验需要连接已配对 TAB5 的 USB。");
        lock(_lifecycle){if(_stopping||Busy)throw new InvalidOperationException("设备仍在处理操作，请等待升级结束。");_installBusy++;}
        try {
            await _usbGate.WaitAsync(token);
            try {
                CloseUsbPort();using var port=Open(device.Port);long previous=-1;
                for(int i=0;i<3;i++) {
                    port.DiscardInBuffer();Send(port,new{version=1,type="tab5_ping"});
                    using var hello=await ReadReplyAsync(port,"tab5_hello",token);
                    previous=Tab5InstallBootCheck.Hello(hello.RootElement,pair.DeviceId,image.Version,previous);
                    if(i<2)await Task.Delay(1200,token);
                }
                Send(port,new{version=1,type="tab5_ota_status"});using var reply=await ReadReplyAsync(port,"tab5_ota_diagnostic",token);
                VerifyUpgradeDiagnostic(reply.RootElement,image);
                string verified=$"verified; firmware={image.Version}; elfSha256={image.ElfSha256}; partition={reply.RootElement.GetProperty("partition").GetString()}; state=VALID";
                _upgradeDiagnostic=verified+"; timing=unavailable";
                // Retained numeric timing survives the OTA reboot. Collect it
                // only after identity, firmware, partition and hash checks pass.
                using var timingStop=CancellationTokenSource.CreateLinkedTokenSource(token);timingStop.CancelAfter(3000);
                try {
                    Send(port,new{version=1,type="tab5_crash_status"});
                    using var trace=await ReadReplyAsync(port,"tab5_crash_diagnostic",timingStop.Token);
                    _upgradeDiagnostic=verified+"; "+FormatUpgradeTiming(trace.RootElement);
                }catch(Exception ex) when(ex is IOException or TimeoutException or OperationCanceledException or JsonException) {
                    if(token.IsCancellationRequested)throw;
                    _upgradeDiagnostic+=$"; timingRead={ex.GetType().Name}";
                }
            }finally{CloseUsbPort();_usbGate.Release();}
        }finally{lock(_lifecycle)_installBusy--;}
    }
    internal static string FormatUpgradeTiming(JsonElement root) {
        if(!root.TryGetProperty("trace",out var trace)||trace.ValueKind!=JsonValueKind.Object)return "timing=unavailable";
        var fields=new List<string>();
        foreach(string key in new[]{"previousStage","previousOffset","previousTotal","previousError","previousStackFree"})
            if(trace.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.Number&&value.TryGetUInt32(out uint number))fields.Add($"{key}={number}");
        foreach(string key in new[]{"otaMs","previousOtaMs"}) {
            if(!trace.TryGetProperty(key,out var values)||values.ValueKind!=JsonValueKind.Array||values.GetArrayLength()!=3)continue;
            var numbers=values.EnumerateArray().ToArray();
            if(numbers.All(x=>x.ValueKind==JsonValueKind.Number&&x.TryGetUInt32(out _)))fields.Add(key+"=["+string.Join(',',numbers.Select(x=>x.GetUInt32()))+"]");
        }
        return fields.Count==0?"timing=unavailable":string.Join("; ",fields);
    }
    internal static void VerifyUpgradeDiagnostic(JsonElement root,Tab5OtaPackage image) {
        string Text(string key)=>root.TryGetProperty(key,out var x)&&x.ValueKind==JsonValueKind.String?x.GetString()!:"";
        int Number(string key)=>root.TryGetProperty(key,out var x)&&x.TryGetInt32(out int n)?n:-1;
        string partition=Text("partition");int address=Number("address");
        if(Text("type")!="tab5_ota_diagnostic"||Text("firmware")!=image.Version||Text("elfSha256")!=image.ElfSha256||
            !(partition=="ota_0"&&address==0x20000||partition=="ota_1"&&address==0x700000)||Number("stateError")!=0||Number("state")!=2)
            throw new IOException("运行镜像、分区或启动确认状态与所选固件不符，不能确认升级成功。");
    }
}
