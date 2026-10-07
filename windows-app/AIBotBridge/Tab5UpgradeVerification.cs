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
        foreach(string key in new[]{"otaPhases","previousOtaPhases"}) {
            if(!trace.TryGetProperty(key,out var phases)||phases.ValueKind!=JsonValueKind.Object||
               !phases.TryGetProperty("transport",out var link)||link.ValueKind!=JsonValueKind.Number||!link.TryGetInt32(out int transport)||transport is < -1 or > 2)continue;
            var phaseFields=new List<string>{$"transport={transport}"};
            foreach(string name in new[]{"prepareMs","writeMs","freeWaitMs","readyWaitMs","verifyMs","installMs"})
                if(phases.TryGetProperty(name,out var number)&&number.ValueKind==JsonValueKind.Number&&number.TryGetUInt32(out uint ms))phaseFields.Add($"{name}={ms}");
            if(phaseFields.Count==7)fields.Add(key+"={"+string.Join(',',phaseFields)+"}");
        }
        string flash=FormatFlashBenchmark(root);if(flash.Length>0)fields.Add(flash);
        return fields.Count==0?"timing=unavailable":string.Join("; ",fields);
    }
    internal static string FormatFlashBenchmark(JsonElement root) {
        if(!root.TryGetProperty("flashBenchmark",out var result)||result.ValueKind!=JsonValueKind.Object)return "";
        var values=new List<string>();
        foreach(string key in new[]{"state","sampleBytes","partitionAddress","offset","jedecId","pageBytes","restored","modified"}) {
            if(!result.TryGetProperty(key,out var field)||field.ValueKind!=JsonValueKind.Number||!field.TryGetUInt32(out uint value))return "";
            if((key=="state"&&value>4)||((key=="restored"||key=="modified")&&value>1))return "";
            values.Add($"{key}={value}");
        }
        if(!result.TryGetProperty("error",out var error)||error.ValueKind!=JsonValueKind.Number||!error.TryGetInt32(out int code))return "";
        values.Add($"error={code}");
        foreach(string key in new[]{"phase","scanned"}) {
            if(!result.TryGetProperty(key,out var field))continue;
            if(field.ValueKind!=JsonValueKind.Number||!field.TryGetUInt32(out uint value)||(key=="phase"&&value>3))return "";
            values.Add($"{key}={value}");
        }
        if(!result.TryGetProperty("rows",out var rows)||rows.ValueKind!=JsonValueKind.Array||rows.GetArrayLength()!=4)return "";
        uint[] chunks=[8192,16384,49152,65536];int i=0;
        foreach(var row in rows.EnumerateArray()) {
            if(row.ValueKind!=JsonValueKind.Object)return "";
            var data=new List<string>();
            foreach(string key in new[]{"chunkBytes","completed","skipped"}) {
                if(!row.TryGetProperty(key,out var field)||field.ValueKind!=JsonValueKind.Number||!field.TryGetUInt32(out uint value)||
                    key=="chunkBytes"&&value!=chunks[i]||key=="completed"&&value>3||key=="skipped"&&value>1)return "";
                data.Add($"{key}={value}");
            }
            foreach(string key in new[]{"eraseUs","writeUs"}) {
                if(!row.TryGetProperty(key,out var array)||array.ValueKind!=JsonValueKind.Array||array.GetArrayLength()!=3)return "";
                var numbers=array.EnumerateArray().ToArray();
                if(numbers.Any(x=>x.ValueKind!=JsonValueKind.Number||!x.TryGetUInt32(out _)))return "";
                data.Add(key+"=["+string.Join(',',numbers.Select(x=>x.GetUInt32()))+"]");
            }
            values.Add($"row{i}={{"+string.Join(',',data)+"}");i++;
        }
        return "flashBenchmark={"+string.Join(',',values)+"}";
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
